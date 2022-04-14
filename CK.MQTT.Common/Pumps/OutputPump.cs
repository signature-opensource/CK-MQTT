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

        /// <summary>
        /// Packet used to wake up the wait
        /// This packet will never be published.
        /// </summary>
        public class FlushPacket : IOutgoingPacket
        {
            public static IOutgoingPacket Instance { get; } = new FlushPacket();
            private FlushPacket() { }
            public ushort PacketId { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public QualityOfService Qos => throw new NotSupportedException();

            public bool IsRemoteOwnedPacketId => throw new NotSupportedException();

            public uint GetSize( ProtocolLevel protocolLevel ) => throw new NotSupportedException();

            public ValueTask<WriteResult> WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken ) => throw new NotSupportedException();
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

        public override Task CloseAsync()
        {
            if( _outputProcessor == null ) throw new NullReferenceException( $"{nameof( _outputProcessor )} is null." );
            _outputProcessor.Stopping();
            return base.CloseAsync();
        }
    }
}
