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
                    var pw = MessageExchanger.Channel.DuplexPipe!.Output;
                    bool packetSent = true;
                    while( packetSent )
                    {
                        if( StopToken.IsCancellationRequested ) break;
                        packetSent = await _outputProcessor.SendPacketsAsync( CloseToken );
                        if( pw.CanGetUnflushedBytes && pw.UnflushedBytes > 50_000 )
                        {
                            await pw.FlushAsync( CloseToken );
                        }
                    }
                    await pw.FlushAsync( CloseToken );
                    if( StopToken.IsCancellationRequested ) return;
                    var res = await MessagesChannel.Reader.WaitToReadAsync( StopToken );
                    if( !res || StopToken.IsCancellationRequested ) return;
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

        public void TryQueueReflexMessage( IOutgoingPacket item )
        {
            void QueuePacket( IOutgoingPacket packet )
            {
                bool result = MessagesChannel.Writer.TryWrite( packet );
                if( !result ) MessageExchanger.Sink.OnQueueFullPacketDropped( packet.PacketId, PacketType.PublishAck );
            }
            if( !item.IsRemoteOwnedPacketId && item.Qos > QualityOfService.AtMostOnce )
            {
                MessageExchanger.LocalPacketStore.BeforeQueueReflexPacket( QueuePacket, item );
            }
            else
            {
                QueuePacket( item );
            }
            UnblockWriteLoop();
        }

        public void UnblockWriteLoop() => MessagesChannel.Writer.TryWrite( FlushPacket.Instance );

        public override async Task CloseAsync()
        {
            var task = MessageExchanger.Channel.DuplexPipe?.Output!.CompleteAsync();
            if( task.HasValue ) await task.Value;
            await base.CloseAsync();
        }
    }
}
