using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CK.MQTT.IOutgoingPacket;

namespace CK.MQTT
{
    public class OutgoingMessageHandler : IDisposable
    {
        public class MessageEventArgs : EventArgs
        {
            public MessageEventArgs( IMqttLogger logger, IOutgoingPacket packet )
            {
                Logger = logger;
                Packet = packet;
            }

            public IMqttLogger Logger { get; }
            public IOutgoingPacket Packet { get; }
        }

        readonly Channel<IOutgoingPacket> _messages;
        readonly Channel<IOutgoingPacket> _reflexes;
        readonly Action<IMqttLogger, DisconnectedReason> _clientClose;
        readonly PipeWriter _pipeWriter;
        readonly MqttConfiguration _config;
        readonly PacketStore _packetStore;
        readonly Task _writeLoop;
        bool _stopped;
        readonly CancellationTokenSource _dirtyStopSource = new CancellationTokenSource();
        public OutgoingMessageHandler( Action<IMqttLogger, DisconnectedReason> clientClose,
            PipeWriter writer, MqttConfiguration config, PacketStore packetStore )
        {
            _messages = Channel.CreateBounded<IOutgoingPacket>( config.ChannelsPacketCount );
            _reflexes = Channel.CreateBounded<IOutgoingPacket>( config.ChannelsPacketCount );
            _clientClose = clientClose;
            _pipeWriter = writer;
            _config = config;
            _packetStore = packetStore;
            _writeLoop = WriteLoop();
        }

        //These will become handy to add telemetry

        public event Action<IMqttLogger, IOutgoingPacket>? OnMessageEmit;
        public event Action<IMqttLogger, IOutgoingPacket>? OnMessageEmitted;

        public bool QueueMessage( IOutgoingPacket item ) => _messages.Writer.TryWrite( item );

        public bool QueueReflexMessage( IOutgoingPacket item ) => _reflexes.Writer.TryWrite( item );

        /// <returns>A <see cref="ValueTask"/> that complete when the packet is sent.</returns>
        public async ValueTask<Task> SendMessageAsync( IOutgoingPacket item )
        {
            var wrapper = new AwaitableOutgoingPacketWrapper( item );
            await _messages.Writer.WriteAsync( wrapper );//ValueTask: most of the time return synchronously
            return wrapper.Sent;//TaskCompletionSource.Task, on some setup will often return synchronously, most of the time, asyncrounously.
        }

        Task<bool> LoopTasks => Task.WhenAny( _reflexes.Reader.WaitToReadAsync().AsTask(), _messages.Reader.WaitToReadAsync().AsTask() ).Unwrap();
        async Task WriteLoop()
        {
            using( _config.OutputLogger.OpenInfo( "Output loop listening..." ) )
            {
                try
                {
                    bool mainLoop = true;
                    while( mainLoop )
                    {
                        if( _dirtyStopSource.IsCancellationRequested ) break;
                        if( _reflexes.Reader.TryRead( out IOutgoingPacket packet ) || _messages.Reader.TryRead( out packet ) )
                        {
                            await ProcessOutgoingPacket( packet );
                            continue;
                        }
                        (int packetId, long waitTime) = _packetStore.IdStore.GetOldestPacket();
                        //0 mean there is no packet in the store.
                        if( packetId != 0 )
                        {
                            if( waitTime < _config.WaitTimeoutMs )
                            {
                                Task<bool> tasks = LoopTasks;
                                await Task.WhenAny( tasks, Task.Delay( (int)(_config.WaitTimeoutMs - waitTime) ) );
                                if( tasks.IsCompleted ) mainLoop = await tasks;
                                continue;
                            }
                            await SendUnackPacket( packetId );
                        }
                        mainLoop = await LoopTasks;
                    }
                    using( _config.OutputLogger.OpenTrace( "Sending remaining messages..." ) )
                    {
                        while( _reflexes.Reader.TryRead( out IOutgoingPacket packet ) ) await ProcessOutgoingPacket( packet );
                        while( _messages.Reader.TryRead( out IOutgoingPacket packet ) ) await ProcessOutgoingPacket( packet );
                    }
                }
                catch( Exception e )
                {
                    _config.OutputLogger.Error( e );
                    CloseInternal( _config.OutputLogger, DisconnectedReason.UnspecifiedError, false );
                    return;
                }
                CloseInternal( _config.OutputLogger, DisconnectedReason.SelfDisconnected, false );
            }
        }
        async Task SendUnackPacket( int packetId )
        {
            if( packetId == 0 ) return;
            IOutgoingPacketWithId packet = await _packetStore.GetMessageByIdAsync( _config.OutputLogger, packetId );
            await _messages.Writer.WriteAsync( packet );
            _packetStore.IdStore.PacketSent( packetId );//We reset the timer, or this packet will be picked up again.
        }

        async ValueTask<WriteResult> ProcessOutgoingPacket( IOutgoingPacket outgoingPacket )
        {
            if( _dirtyStopSource.IsCancellationRequested ) return WriteResult.Cancelled;
            using( _config.OutputLogger.OpenInfo( $"Sending message '{outgoingPacket}' of size {outgoingPacket.Size}." ) )
            {
                OnMessageEmit?.Invoke( _config.OutputLogger, outgoingPacket );
                WriteResult result = await outgoingPacket.WriteAsync( _pipeWriter, _dirtyStopSource.Token );
                OnMessageEmitted?.Invoke( _config.OutputLogger, outgoingPacket );
                if( outgoingPacket is IOutgoingPacketWithId packetWithId )
                {
                    _packetStore.IdStore.PacketSent( packetWithId.PacketId );
                }
                return result;
            }
        }

        bool _complete;
        public Task Complete()
        {
            lock( _writeLoop )
            {
                if( _complete ) return _writeLoop;
                _complete = true;
            }
            _messages.Writer.Complete();
            _reflexes.Writer.Complete();
            return _writeLoop;
        }

        void CloseInternal( IMqttLogger m, DisconnectedReason disconnectedReason, bool waitLoop )
        {
            lock( _writeLoop )
            {
                if( _stopped ) return;
                _stopped = true;
            }
            m.Trace( $"Closing {nameof( OutgoingMessageHandler )}." );
            var task = Complete();
            _dirtyStopSource.Cancel();
            if( waitLoop && !task.IsCompleted )
            {
                m.Warn( $"{nameof( OutgoingMessageHandler )} main loop is taking time to exit..." );
                task.Wait();
            }
            _clientClose( m, disconnectedReason );
        }

        public void Dispose() => _pipeWriter.Complete();

        public void Close( IMqttLogger m, DisconnectedReason disconnectedReason ) => CloseInternal( m, disconnectedReason, true );
    }
}
