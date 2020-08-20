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
    public class OutgoingMessageHandler
    {
        readonly Channel<IOutgoingPacket> _messages;
        readonly Channel<IOutgoingPacket> _reflexes;
        readonly PingRespReflex _pingRespReflex;
        readonly Func<IMqttLogger, DisconnectedReason, Task> _clientClose;
        readonly PipeWriter _pipeWriter;
        readonly MqttConfiguration _config;
        readonly PacketStore _packetStore;
        readonly Task _writeLoopTask;
        readonly CancellationTokenSource _stopSource = new CancellationTokenSource();
        /// <summary>
        /// Instantiate a new <see cref="OutgoingMessageHandler"/>.
        /// </summary>
        /// <param name="clientClose">A <see langword="delegate"/> that will be called when the pump close.</param>
        /// <param name="writer">The pipe where the pump will write the messages to.</param>
        /// <param name="config">The config to use.</param>
        /// <param name="packetStore">The packet store to use to retrieve packets.</param>
        public OutgoingMessageHandler(
            PingRespReflex pingRespReflex,
            Func<IMqttLogger, DisconnectedReason, Task> clientClose,
            PipeWriter writer, MqttConfiguration config, PacketStore packetStore )
        {
            _messages = Channel.CreateBounded<IOutgoingPacket>( config.ChannelsPacketCount );
            _reflexes = Channel.CreateBounded<IOutgoingPacket>( config.ChannelsPacketCount );
            _pingRespReflex = pingRespReflex;
            _clientClose = clientClose;
            _pipeWriter = writer;
            _config = config;
            _packetStore = packetStore;
            _writeLoopTask = WriteLoop();
        }

        public bool QueueMessage( IOutgoingPacket item ) => _messages.Writer.TryWrite( item );

        public bool QueueReflexMessage( IOutgoingPacket item ) => _reflexes.Writer.TryWrite( item );

        /// <returns>A <see cref="Task"/> that complete when the packet is sent.</returns>
        public async ValueTask<Task> SendMessageAsync( IOutgoingPacket item )
        {
            var wrapper = new AwaitableOutgoingPacketWrapper( item );
            await _messages.Writer.WriteAsync( wrapper );//ValueTask: most of the time return synchronously
            return wrapper.Sent;//TaskCompletionSource.Task, on some setup will often return synchronously, most of the time, asyncrounously.
        }


        async ValueTask<bool> SendAMessageFromQueue( IMqttLogger m )
        {
            if( _stopSource.IsCancellationRequested ) return false;
            if( !_reflexes.Reader.TryRead( out IOutgoingPacket packet ) && !_messages.Reader.TryRead( out packet ) ) return false;
            await ProcessOutgoingPacket( m, packet );
            return true;
        }

        static Task NeverTask { get; } = new TaskCompletionSource<object>().Task;

        async ValueTask<Task> ResendUnackPacket( IMqttLogger m )
        {
            while( true )
            {
                (int packetId, long waitTime) = _packetStore.IdStore.GetOldestPacket();
                //0 mean there is no packet in the store. So we don't want to wake up the loop to resend packets.
                if( packetId == 0 ) return NeverTask;//Loop will complete another task when a new packet will be sent.
                //Wait the right amount of time
                if( waitTime < _config.WaitTimeoutMs ) return Task.Delay( (int)(_config.WaitTimeoutMs - waitTime) );
                await SendUnackPacket( m, packetId );
            }
        }

        async Task WriteLoop()
        {
            using( _config.OutputLogger.OpenInfo( "Output loop listening..." ) )
            {
                try
                {
                    Task cancelTask = Task.FromCanceled( _stopSource.Token );
                    while( !_stopSource.IsCancellationRequested )
                    {
                        IMqttLogger m = _config.OutputLogger;
                        bool messageSent = await SendAMessageFromQueue( m );
                        Task resendTask = await ResendUnackPacket( m );
                        if( resendTask.IsCompleted || messageSent ) continue;//A message has been sent, skip keepAlive logic.
                        //We didn't sent any message. We start a KeepAlive.
                        Task keepAliveTask = _config.KeepAliveSecs != 0 ? Task.Delay( _config.KeepAliveSecs ) : NeverTask;
                        await await Task.WhenAny(
                            _reflexes.Reader.WaitToReadAsync().AsTask(),
                            _messages.Reader.WaitToReadAsync().AsTask(),
                            resendTask,
                            keepAliveTask,
                            cancelTask
                            );
                        if( keepAliveTask.IsCompleted )
                        {
                            await ProcessOutgoingPacket( m, OutgoingPingReq.Instance );
                            _pingRespReflex.StartPingTimeoutTimer();
                        }
                    }
                    _pipeWriter.Complete();
                }
                catch( Exception e )
                {
                    _config.OutputLogger.Error( "Error while writing data.", e );
                    _stopSource.Cancel();
                    await _clientClose( _config.OutputLogger, DisconnectedReason.UnspecifiedError );
                }
            }
        }
        async Task SendUnackPacket( IMqttLogger m, int packetId )
        {
            if( packetId == 0 ) return;
            IOutgoingPacketWithId packet = await _packetStore.GetMessageByIdAsync( m, packetId );
            await _messages.Writer.WriteAsync( packet, _stopSource.Token );
            _packetStore.IdStore.PacketSent( m, packetId );//We reset the timer, or this packet will be picked up again.
        }

        async ValueTask<WriteResult> ProcessOutgoingPacket( IMqttLogger m, IOutgoingPacket outgoingPacket )
        {
            if( _stopSource.IsCancellationRequested ) return WriteResult.Cancelled;
            using( m.OpenInfo( $"Sending message '{outgoingPacket}' of size {outgoingPacket.Size}." ) )
            {
                WriteResult result = await outgoingPacket.WriteAsync( _pipeWriter, _stopSource.Token );
                if( outgoingPacket is IOutgoingPacketWithId packetWithId )
                {
                    _packetStore.IdStore.PacketSent( m, packetWithId.PacketId );
                }
                return result;
            }
        }

        public Task CloseAsync()
        {
            if( _stopSource.IsCancellationRequested ) return Task.CompletedTask;//Allow to not await ourself.
            _stopSource.Cancel();
            return _writeLoopTask;
        }
    }
}
