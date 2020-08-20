using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CK.MQTT.IOutgoingPacket;

namespace CK.MQTT
{
    /// <summary>
    /// The message pump that serialize the messages to the <see cref="PipeWriter"/>.
    /// Accept messages concurrently, but it will send them one per one.
    /// </summary>
    public class OutgoingMessageHandler : IDisposable
    {
        readonly Channel<IOutgoingPacket> _messages;
        readonly Channel<IOutgoingPacket> _reflexes;
        readonly IncomingMessageHandler _incomingMessageHandler;
        readonly Action<IMqttLogger, DisconnectedReason> _clientClose;
        readonly PipeWriter _pipeWriter;
        readonly MqttConfiguration _config;
        readonly PacketStore _packetStore;
        readonly Task _writeLoopTask;
        bool _stopped;
        readonly CancellationTokenSource _dirtyStopSource = new CancellationTokenSource();
        /// <summary>
        /// Instantiate a new <see cref="OutgoingMessageHandler"/>.
        /// </summary>
        /// <param name="clientClose">A <see langword="delegate"/> that will be called when the pump close.</param>
        /// <param name="writer">The pipe where the pump will write the messages to.</param>
        /// <param name="config">The config to use.</param>
        /// <param name="packetStore">The packet store to use to retrieve packets.</param>
        public OutgoingMessageHandler(
            IncomingMessageHandler incomingMessageHandler,
            Action<IMqttLogger, DisconnectedReason> clientClose,
            PipeWriter writer, MqttConfiguration config, PacketStore packetStore )
        {
            _messages = Channel.CreateBounded<IOutgoingPacket>( config.ChannelsPacketCount );
            _reflexes = Channel.CreateBounded<IOutgoingPacket>( config.ChannelsPacketCount );
            _incomingMessageHandler = incomingMessageHandler;
            _clientClose = clientClose;
            _pipeWriter = writer;
            _config = config;
            _packetStore = packetStore;
            _writeLoopTask = WriteLoop();
        }

        public bool QueueMessage( IOutgoingPacket item ) => _messages.Writer.TryWrite( item );

        public bool QueueReflexMessage( IOutgoingPacket item ) => _reflexes.Writer.TryWrite( item );

        /// <returns>A <see cref="ValueTask"/> that complete when the packet is sent.</returns>
        public async ValueTask<Task> SendMessageAsync( IOutgoingPacket item )
        {
            var wrapper = new AwaitableOutgoingPacketWrapper( item );
            await _messages.Writer.WriteAsync( wrapper );//ValueTask: most of the time return synchronously
            return wrapper.Sent;//TaskCompletionSource.Task, on some setup will often return synchronously, most of the time, asyncrounously.
        }


        async ValueTask<bool> SendAMessageFromQueue()
        {
            if( _dirtyStopSource.IsCancellationRequested ) return false;
            if( !_reflexes.Reader.TryRead( out IOutgoingPacket packet ) && !_messages.Reader.TryRead( out packet ) ) return false;
            await ProcessOutgoingPacket( packet );
            return true;
        }

        static readonly Task _neverTask = new TaskCompletionSource<object>().Task;

        async ValueTask<Task> ResendUnackPacket()
        {
            while( true )
            {
                (int packetId, long waitTime) = _packetStore.IdStore.GetOldestPacket();
                //0 mean there is no packet in the store. So we don't want to wake up the loop to resend packets.
                if( packetId == 0 ) return _neverTask;//Loop will complete another task when a new packet will be sent.
                //Wait the right amount of time
                if( waitTime < _config.WaitTimeoutMs ) return Task.Delay( (int)(_config.WaitTimeoutMs - waitTime) );
                await SendUnackPacket( packetId );
            }
        }

        async Task WriteLoop()
        {
            using( _config.OutputLogger.OpenInfo( "Output loop listening..." ) )
            {
                try
                {
                    while( !_dirtyStopSource.IsCancellationRequested )
                    {
                        bool messageSent = await SendAMessageFromQueue();
                        Task resendTask = await ResendUnackPacket();
                        if( resendTask.IsCompleted || messageSent ) continue;//A message has been sent, skip keepAlive logic.
                        //We didn't sent any message. We start a KeepAlive.
                        Task keepAliveTask = Task.Delay( _config.KeepAliveSecs );
                        await Task.WhenAny(
                            _reflexes.Reader.WaitToReadAsync().AsTask(),
                            _messages.Reader.WaitToReadAsync().AsTask(),
                            resendTask,
                            keepAliveTask ).Unwrap();
                        if( keepAliveTask.IsCompleted )
                        {
                            await ProcessOutgoingPacket( OutgoingPingReq.Instance );
                        }

                    }
                }
                catch( Exception e )
                {
                    _config.OutputLogger.Error( e );
                    CloseInternal( _config.OutputLogger, DisconnectedReason.UnspecifiedError, false );
                }
            }
        }
        async Task SendUnackPacket( IMqttLogger m, int packetId )
        {
            if( packetId == 0 ) return;
            IOutgoingPacketWithId packet = await _packetStore.GetMessageByIdAsync( _config.OutputLogger, packetId );
            await _messages.Writer.WriteAsync( packet );
            _packetStore.IdStore.PacketSent( m, packetId );//We reset the timer, or this packet will be picked up again.
        }

        async ValueTask<WriteResult> ProcessOutgoingPacket( IMqttLogger m, IOutgoingPacket outgoingPacket )
        {
            if( _dirtyStopSource.IsCancellationRequested ) return WriteResult.Cancelled;
            using( _config.OutputLogger.OpenInfo( $"Sending message '{outgoingPacket}' of size {outgoingPacket.Size}." ) )
            {
                WriteResult result = await outgoingPacket.WriteAsync( _pipeWriter, _dirtyStopSource.Token );
                if( outgoingPacket is IOutgoingPacketWithId packetWithId )
                {
                    _packetStore.IdStore.PacketSent( m, packetWithId.PacketId );
                }
                return result;
            }
        }

        readonly object _stopLock = new object();
        void CloseInternal( IMqttLogger m, DisconnectedReason disconnectedReason, bool waitLoop )
        {
            if( _stopped ) return;
            _stopped = true;
            m.Trace( $"Closing {nameof( OutgoingMessageHandler )}." );
            _messages.Writer.Complete();
            _reflexes.Writer.Complete();
            _dirtyStopSource.Cancel();
            if( waitLoop && !_writeLoopTask.IsCompleted )
            {
                m.Warn( $"{nameof( OutgoingMessageHandler )} write loop is taking time to exit..." );
                _writeLoopTask.Wait();
            }
            _clientClose( m, disconnectedReason );
        }

        public void Dispose() => _pipeWriter.Complete();

        public void Close( IMqttLogger m, DisconnectedReason disconnectedReason ) => CloseInternal( m, disconnectedReason, true );
    }
}
