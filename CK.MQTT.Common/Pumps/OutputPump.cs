using CK.Core;
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
        readonly ILocalPacketStore _outgoingPacketStore;


        /// <summary>
        /// Instantiates a new <see cref="OutputPump"/>.
        /// </summary>
        /// <param name="writer">The pipe where the pump will write the messages to.</param>
        /// <param name="store">The packet store to use to retrieve packets.</param>
        public OutputPump( ILocalPacketStore outgoingPacketStore, Func<DisconnectedReason, ValueTask> onDisconnect, MqttConfigurationBase config ) : base( onDisconnect )
        {
            _outgoingPacketStore = outgoingPacketStore;
            Config = config;
            MessagesChannel = Channel.CreateBounded<IOutgoingPacket>( new BoundedChannelOptions( Config.OutgoingPacketsChannelCapacity )
            {
                SingleReader = true
            } );
            ReflexesChannel = Channel.CreateBounded<IOutgoingPacket>( new BoundedChannelOptions( Config.OutgoingPacketsChannelCapacity )
            {
                SingleReader = true
            } );
        }

        public Channel<IOutgoingPacket> MessagesChannel { get; }
        public Channel<IOutgoingPacket> ReflexesChannel { get; }
        public MqttConfigurationBase Config { get; }

        public void StartPumping( OutputProcessor outputProcessor )
        {
            _outputProcessor = outputProcessor;
            SetRunningLoop( WriteLoopAsync() );
        }

        async Task WriteLoopAsync()
        {
            Debug.Assert( _outputProcessor != null ); // TODO: Put non nullable init on output processor when it will be available.
            using( Config.OutputLogger?.OutputLoopStarting() )
            {
                try
                {
                    while( !StopToken.IsCancellationRequested )
                    {
                        bool packetSent = await _outputProcessor.SendPacketsAsync( Config.OutputLogger, CloseToken );
                        if( !packetSent )
                        {
                            if( StopToken.IsCancellationRequested ) return;
                            await _outputProcessor.WaitPacketAvailableToSendAsync( Config.OutputLogger, CancellationToken.None, StopToken );
                        }
                    }
                }
                catch( OperationCanceledException e )
                {
                    Config.OutputLogger?.OutputLoopCancelled( e );
                }
                catch( Exception e )
                {
                    Config.OutputLogger?.ExceptionInOutputLoop( e );
                    await SelfCloseAsync( DisconnectedReason.InternalException );
                }
            }
        }

        /// <summary>
        /// Packet used to wake up the wait
        /// Packet will never be published.
        /// </summary>
        public class TriggerPacket : IOutgoingPacket
        {
            public static IOutgoingPacket Instance { get; } = new TriggerPacket();
            private TriggerPacket() { }
            public uint PacketId { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public QualityOfService Qos => throw new NotSupportedException();

            public bool IsRemoteOwnedPacketId => throw new NotSupportedException();

            public uint GetSize( ProtocolLevel protocolLevel ) => throw new NotSupportedException();

            public ValueTask<IOutgoingPacket.WriteResult> WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken ) => throw new NotSupportedException();
        }
        public void QueueReflexMessage( IInputLogger? m, IOutgoingPacket item )
        {
            void QueuePacket( IOutgoingPacket packet )
            {
                bool result = ReflexesChannel.Writer.TryWrite( item );
                if( !result ) m?.QueueFullPacketDropped( PacketType.PublishAck, item.PacketId );
            }
            if( !item.IsRemoteOwnedPacketId )
            {

                _outgoingPacketStore.BeforeQueueReflexPacket( m, QueuePacket, item );
            }
            else
            {
                QueuePacket( item );
            }

        }

        /// <returns>A <see cref="Task"/> that complete when the packet is sent.</returns>
        public async Task QueueMessageAndWaitUntilSentAsync( IActivityMonitor? m, IOutgoingPacket packet )
        {
            using( m?.OpenTrace( "Sending a message ..." ) )
            {
                var wrapper = new AwaitableOutgoingPacketWrapper( packet );
                m?.Trace( $"Queuing the packet '{packet}'. " );
                await MessagesChannel.Writer.WriteAsync( wrapper );//ValueTask: most of the time return synchronously
                //if( ReflexesChannel.Reader.Count == 0 )
                {
                    ReflexesChannel.Writer.TryWrite( TriggerPacket.Instance );
                }
                m?.Trace( $"Awaiting that the packet '{packet}' has been written." );
                await wrapper.Sent;//TaskCompletionSource.Task, on some setup will often return synchronously, most of the time, asynchronously.
            }
        }

        public override Task CloseAsync()
        {
            if( _outputProcessor == null ) throw new NullReferenceException( $"{nameof( _outputProcessor )} is null." );
            _outputProcessor.Stopping();
            return base.CloseAsync();
        }
    }
}
