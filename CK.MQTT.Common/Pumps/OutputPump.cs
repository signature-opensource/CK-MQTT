using CK.Core;
using CK.MQTT.Stores;
using Microsoft.Toolkit.HighPerformance.Extensions;
using System;
using System.Diagnostics.CodeAnalysis;
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
        public delegate ValueTask PacketSender( IOutputLogger? m, IOutgoingPacket outgoingPacket );

        internal Channel<IOutgoingPacket> MessagesChannel { get; }
        internal Channel<IOutgoingPacket> ReflexesChannel { get; }
        OutputProcessor _outputProcessor;
        public ProtocolConfiguration PConfig { get; }
        public MqttConfigurationBase Config { get; }
        public IOutgoingPacketStore Store { get; }
        CancellationTokenSource _processorStopSource = new CancellationTokenSource();

        /// <summary>
        /// Instantiates a new <see cref="OutputPump"/>.
        /// </summary>
        /// <param name="writer">The pipe where the pump will write the messages to.</param>
        /// <param name="store">The packet store to use to retrieve packets.</param>
        public OutputPump( PumppeteerBase pumppeteer, ProtocolConfiguration pconfig, OutputProcessor initialProcessor, PipeWriter writer, IOutgoingPacketStore store )
            : base( pumppeteer )
        {

            (PConfig, Config, Store, _outputProcessor) = (pconfig, pumppeteer.Configuration, store, initialProcessor);
            MessagesChannel = Channel.CreateBounded<IOutgoingPacket>( new BoundedChannelOptions( Config.OutgoingPacketsChannelCapacity )
            {
                SingleReader = true
            } );
            ReflexesChannel = Channel.CreateBounded<IOutgoingPacket>( new BoundedChannelOptions( Config.OutgoingPacketsChannelCapacity )
            {
                SingleReader = true
            } );
            SetRunningLoop( WriteLoop() );
        }

        [MemberNotNull( nameof( _outputProcessor ) )]
        public void SetOutputProcessor( OutputProcessor outputProcessor )
        {
            _processorStopSource.Cancel();
            _processorStopSource = new CancellationTokenSource();
            _outputProcessor = outputProcessor;
        }

        public bool QueueMessage( IOutgoingPacket item ) => MessagesChannel.Writer.TryWrite( item );

        public bool QueueReflexMessage( IOutgoingPacket item ) => ReflexesChannel.Writer.TryWrite( item );

        /// <returns>A <see cref="Task"/> that complete when the packet is sent.</returns>
        public async Task SendMessageAsync( IActivityMonitor? m, IOutgoingPacket packet )
        {
            using( m?.OpenTrace( "Sending a message ..." ) )
            {
                var wrapper = new AwaitableOutgoingPacketWrapper( packet );
                m?.Trace( $"Queuing the packet '{packet}'. " );
                await MessagesChannel.Writer.WriteAsync( wrapper );//ValueTask: most of the time return synchronously
                m?.Trace( $"Awaiting that the packet '{packet}' has been written." );
                await wrapper.Sent;//TaskCompletionSource.Task, on some setup will often return synchronously, most of the time, asynchronously.
            }
        }


        async Task WriteLoop()
        {
            using( Config.OutputLogger?.OutputLoopStarting() )
            {
                try
                {
                    while( !StopToken.IsCancellationRequested )
                    {
                        bool packetSent = await _outputProcessor.SendPackets( Config.OutputLogger, _processorStopSource.Token );
                        if( !packetSent )
                        {
                            await _outputProcessor.WaitPacketAvailableToSendAsync( Config.OutputLogger, StopToken );
                        }
                    }
                    _outputProcessor.Stopping();
                }
                catch( Exception e )
                {
                    Config.OutputLogger?.ExceptionInOutputLoop( e );
                    await DisconnectAsync( DisconnectedReason.UnspecifiedError );
                }
            }
        }

        protected override Task OnClosedAsync( Task loop )
        {
            _processorStopSource?.Cancel();
            return loop;
        }
    }
}
