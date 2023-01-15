using CK.MQTT.Client;
using CK.MQTT.Packets;
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
        readonly PipeWriter _pipeWriter;
        readonly IMQTT3Sink _sink;

        /// <summary>
        /// Instantiates a new <see cref="OutputPump"/>.
        /// </summary>
        /// <param name="writer">The pipe where the pump will write the messages to.</param>
        /// <param name="store">The packet store to use to retrieve packets.</param>
        public OutputPump( IMQTT3Sink sink,
                          PipeWriter pipeWriter,
                          Func<DisconnectReason, ValueTask> closeHandler,
                          int channelCapacity ) : base( closeHandler )
        {
            _messagesChannel = Channel.CreateBounded<IOutgoingPacket>( new BoundedChannelOptions( channelCapacity )
            {
                SingleReader = true
            } );
            _reflexesChannel = Channel.CreateBounded<IOutgoingPacket>( new BoundedChannelOptions( channelCapacity )
            {
                SingleReader = true
            } );
            _pipeWriter = pipeWriter;
            _sink = sink;
        }

        public OutputProcessor OutputProcessor { get; set; } = null!;

        public ChannelReader<IOutgoingPacket> MessagesChannel => _messagesChannel.Reader;
        public ChannelReader<IOutgoingPacket> ReflexesChannel => _reflexesChannel.Reader;

        protected override async Task WorkLoopAsync( CancellationToken stopToken, CancellationToken closeToken )
        {
            try
            {
                if( _pipeWriter is null )
                {
                    Debug.Assert( stopToken.IsCancellationRequested );
                    return;
                }
                while( !stopToken.IsCancellationRequested )
                {
                    bool packetSent = true;
                    while( packetSent )
                    {
                        if( stopToken.IsCancellationRequested ) break;
                        packetSent = await OutputProcessor.SendPacketsAsync( closeToken );
                        if( _pipeWriter.CanGetUnflushedBytes && _pipeWriter.UnflushedBytes > 50_000 )
                        {
                            await _pipeWriter.FlushAsync( closeToken );
                        }
                    }
                    await _pipeWriter.FlushAsync( closeToken );
                    if( stopToken.IsCancellationRequested ) return;
                    var res = await MessagesChannel.WaitToReadAsync( stopToken );
                    if( !res || stopToken.IsCancellationRequested ) return;
                }
                await _pipeWriter.CompleteAsync();
            }
            catch( OperationCanceledException e )
            {
                if( _pipeWriter != null ) await _pipeWriter.CompleteAsync( e );
            }
            catch( Exception e )
            {
                if( _pipeWriter != null ) await _pipeWriter.CompleteAsync( e );
                await SelfDisconnectAsync( DisconnectReason.InternalException );
            }
        }

        public void TryQueueReflexMessage( IOutgoingPacket item )
        {
            bool result = _reflexesChannel.Writer.TryWrite( item );
            if( !result ) _sink.OnQueueFullPacketDropped( item.PacketId );
            UnblockWriteLoop();
        }

        public void TryQueueMessage( IOutgoingPacket item )
        {
            bool result = _messagesChannel.Writer.TryWrite( item );
            if( !result ) _sink.OnQueueFullPacketDropped( item.Qos == QualityOfService.AtMostOnce ? (ushort)0 : item.PacketId, item.Type );
        }

        public ValueTask QueueMessageAsync( IOutgoingPacket item )
            => _messagesChannel.Writer.WriteAsync( item );

        public void UnblockWriteLoop() => _messagesChannel.Writer.TryWrite( FlushPacket.Instance );

    }
}
