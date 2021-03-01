using CK.Core;
using CK.MQTT.Stores;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// The message pump that serializes the messages to the <see cref="PipeWriter"/>.
    /// Accept messages concurrently, but send them one per one.
    /// </summary>
    public class OutputPump : PumpBase
    {
        public delegate ValueTask OutputProcessor( IOutputLogger? m, PacketSender packetSender, Channel<IOutgoingPacket> reflexes, Channel<IOutgoingPacket> messages, Func<DisconnectedReason, Task<bool>> clientClose, CancellationToken cancellationToken );

        public delegate ValueTask PacketSender( IOutputLogger? m, IOutgoingPacket outgoingPacket );

        readonly Channel<IOutgoingPacket> _messages;
        readonly Channel<IOutgoingPacket> _reflexes;
        OutputProcessor _outputProcessor;
        readonly PipeWriter _pipeWriter;
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

            (_pipeWriter, PConfig, Config, Store, _outputProcessor) = (writer, pconfig, pumppeteer.Configuration, store, initialProcessor);
            _messages = Channel.CreateBounded<IOutgoingPacket>( new BoundedChannelOptions( Config.OutgoingPacketsChannelCapacity )
            {
                SingleReader = true
            } );
            _reflexes = Channel.CreateBounded<IOutgoingPacket>( new BoundedChannelOptions( Config.OutgoingPacketsChannelCapacity )
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

        public bool QueueMessage( IOutgoingPacket item ) => _messages.Writer.TryWrite( item );

        public bool QueueReflexMessage( IOutgoingPacket item ) => _reflexes.Writer.TryWrite( item );

        /// <returns>A <see cref="Task"/> that complete when the packet is sent.</returns>
        public async Task SendMessageAsync( IActivityMonitor? m, IOutgoingPacket packet )
        {
            using( m?.OpenTrace( "Sending a message ..." ) )
            {
                var wrapper = new AwaitableOutgoingPacketWrapper( packet );
                m?.Trace( $"Queuing the packet '{packet}'. " );
                await _messages.Writer.WriteAsync( wrapper );//ValueTask: most of the time return synchronously
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
                        await _outputProcessor( Config.OutputLogger, ProcessOutgoingPacket, _reflexes, _messages, DisconnectAsync, _processorStopSource.Token );
                    }
                    _pipeWriter.Complete();
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
