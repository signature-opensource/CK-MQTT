using CK.MQTT.Client;
using CK.MQTT.Packets;
using CK.MQTT.Stores;
using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Pumps
{
    /// <summary>
    /// The message pump that serializes the messages to the <see cref="PipeWriter"/>.
    /// Accept messages concurrently, but send them one per one.
    /// </summary>
    public class OutputPump : PumpBase
    {
        OutputProcessor? _outputProcessor;

        /// <summary>
        /// Instantiates a new <see cref="OutputPump"/>.
        /// </summary>
        /// <param name="writer">The pipe where the pump will write the messages to.</param>
        /// <param name="store">The packet store to use to retrieve packets.</param>
        public OutputPump( MessageExchanger messageExchanger ) : base( messageExchanger )
        {
            MessagesChannel = Channel.CreateBounded<IOutgoingPacket>( new BoundedChannelOptions( MessageExchanger.Config.OutgoingPacketsChannelCapacity )
            {
                SingleReader = true
            } );
            ReflexesChannel = Channel.CreateBounded<IOutgoingPacket>( new BoundedChannelOptions( MessageExchanger.Config.OutgoingPacketsChannelCapacity )
            {
                SingleReader = true
            } );
        }

        public Channel<IOutgoingPacket> MessagesChannel { get; }
        public Channel<IOutgoingPacket> ReflexesChannel { get; }

        public void StartPumping( OutputProcessor outputProcessor )
        {
            _outputProcessor = outputProcessor;
            SetRunningLoop( WriteLoopAsync() );
        }

        async Task WriteLoopAsync()
        {
            Debug.Assert( _outputProcessor != null ); // TODO: Put non nullable init on output processor when it will be available.
            try
            {
                while( !StopToken.IsCancellationRequested )
                {
                    bool packetSent = await _outputProcessor.SendPacketsAsync( CloseToken );
                    if( !packetSent )
                    {
                        if( StopToken.IsCancellationRequested ) return;
                        await _outputProcessor.WaitPacketAvailableToSendAsync( CancellationToken.None, StopToken );
                    }
                }
            }
            catch( OperationCanceledException )
            {
            }
            catch( Exception )
            {
                await SelfCloseAsync( DisconnectReason.InternalException );
            }
        }

        public void QueueReflexMessage( IOutgoingPacket item )
        {
            void QueuePacket( IOutgoingPacket packet )
            {
                bool result = ReflexesChannel.Writer.TryWrite( item );
                if( !result ) MessageExchanger.Sink.OnQueueFullPacketDropped( item.PacketId, PacketType.PublishAck );
            }
            if( !item.IsRemoteOwnedPacketId )
            {

                MessageExchanger.LocalPacketStore.BeforeQueueReflexPacket( QueuePacket, item );
            }
            else
            {
                QueuePacket( item );
            }

        }

        /// <returns>A <see cref="Task"/> that complete when the packet is sent.</returns>
        [ThreadColor( ThreadColor.Rainbow )]
        public async Task QueueMessageAndWaitUntilSentAsync( IOutgoingPacket packet )
        {
            var wrapper = new AwaitableOutgoingPacketWrapper( packet );
            await MessagesChannel.Writer.WriteAsync( wrapper );//ValueTask: most of the time return synchronously
            if( ReflexesChannel.Reader.Count == 0 )
            {
                ReflexesChannel.Writer.TryWrite( FlushPacket.Instance );
            }
            await wrapper.Sent;//TaskCompletionSource.Task, on some setup will often return synchronously, most of the time, asynchronously.
        }

        public override async Task CloseAsync()
        {
            var task = MessageExchanger.Channel.DuplexPipe?.Output!.CompleteAsync();
            if( task.HasValue ) await task.Value;
            await base.CloseAsync();
        }
    }
}