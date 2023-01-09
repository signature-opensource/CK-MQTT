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
        readonly Channel<IOutgoingPacket> _messagesChannel;
        readonly Channel<IOutgoingPacket> _reflexesChannel;
        OutputProcessor? _outputProcessor;
        /// <summary>
        /// Instantiates a new <see cref="OutputPump"/>.
        /// </summary>
        /// <param name="writer">The pipe where the pump will write the messages to.</param>
        /// <param name="store">The packet store to use to retrieve packets.</param>
        public OutputPump( MessageExchanger messageExchanger ) : base( messageExchanger )
        {
            _messagesChannel = Channel.CreateBounded<IOutgoingPacket>( new BoundedChannelOptions( MessageExchanger.Config.OutgoingPacketsChannelCapacity )
            {
                SingleReader = true
            } );
            _reflexesChannel = Channel.CreateBounded<IOutgoingPacket>( new BoundedChannelOptions( MessageExchanger.Config.OutgoingPacketsChannelCapacity )
            {
                SingleReader = true
            } );
        }


        public ChannelReader<IOutgoingPacket> MessagesChannel => _messagesChannel.Reader;
        public ChannelReader<IOutgoingPacket> ReflexesChannel => _reflexesChannel.Reader;

        public void StartPumping( OutputProcessor outputProcessor )
        {
            _outputProcessor = outputProcessor;
            SetRunningLoop( WriteLoopAsync() );
            _outputProcessor.Starting();
        }

        async Task WriteLoopAsync()
        {
            Debug.Assert( _outputProcessor != null ); // TODO: Put non nullable init on output processor when it will be available.
            PipeWriter? pw = null;
            try
            {
                pw = MessageExchanger.Channel.DuplexPipe?.Output;
                if( pw is null )
                {
                    Debug.Assert( MessageExchanger.StopTokenSource.IsCancellationRequested );
                    return;
                }
                while( !MessageExchanger.StopTokenSource.IsCancellationRequested )
                {
                    bool packetSent = true;
                    while( packetSent )
                    {
                        if( MessageExchanger.StopTokenSource.IsCancellationRequested ) break;
                        packetSent = await _outputProcessor.SendPacketsAsync( MessageExchanger.StopTokenSource.Token );
                        if( pw.CanGetUnflushedBytes && pw.UnflushedBytes > 50_000 )
                        {
                            await pw.FlushAsync( MessageExchanger.StopTokenSource.Token );
                        }
                    }
                    await pw.FlushAsync( MessageExchanger.StopTokenSource.Token );
                    if( MessageExchanger.StopTokenSource.Token.IsCancellationRequested ) return;
                    var res = await MessagesChannel.WaitToReadAsync( MessageExchanger.StopTokenSource.Token );
                    if( !res || MessageExchanger.StopTokenSource.IsCancellationRequested ) return;
                }
                await pw.CompleteAsync();
            }
            catch( OperationCanceledException e )
            {
                if( pw != null ) await pw.CompleteAsync( e );
            }
            catch( Exception e )
            {
                if( pw != null ) await pw.CompleteAsync( e );
                await SelfCloseAsync( DisconnectReason.InternalException );
            }
        }

        public void TryQueueReflexMessage( IOutgoingPacket item )
        {
            bool result = _reflexesChannel.Writer.TryWrite( item );
            if( !result ) MessageExchanger.Sink.OnQueueFullPacketDropped( item.PacketId );
            UnblockWriteLoop();
        }

        public void TryQueueMessage( IOutgoingPacket item )
        {
            bool result = _messagesChannel.Writer.TryWrite( item );
            if( !result ) MessageExchanger.Sink.OnQueueFullPacketDropped( item.Qos == QualityOfService.AtMostOnce ? (ushort)0 : item.PacketId, item.Type );
        }

        public ValueTask QueueMessageAsync( IOutgoingPacket item )
            => _messagesChannel.Writer.WriteAsync( item );

        public void UnblockWriteLoop() => _messagesChannel.Writer.TryWrite( FlushPacket.Instance );

    }
}
