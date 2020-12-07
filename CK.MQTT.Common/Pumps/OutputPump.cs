using System;
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
        readonly ProtocolConfiguration _pconfig;
        readonly MqttConfigurationBase _config;
        readonly PacketStore _packetStore;
        CancellationTokenSource _processorStopSource = new CancellationTokenSource();

        /// <summary>
        /// Instantiates a new <see cref="OutputPump"/>.
        /// </summary>
        /// <param name="writer">The pipe where the pump will write the messages to.</param>
        /// <param name="packetStore">The packet store to use to retrieve packets.</param>
        public OutputPump( Pumppeteer pumppeteer, ProtocolConfiguration pconfig, OutputProcessor initialProcessor, PipeWriter writer, PacketStore packetStore )
            : base( pumppeteer )
        {

            (_pipeWriter, _pconfig, _config, _packetStore, _outputProcessor) = (writer, pconfig, pumppeteer.Configuration, packetStore, initialProcessor);
            _messages = Channel.CreateBounded<IOutgoingPacket>( _config.OutgoingPacketsChannelCapacity );
            _reflexes = Channel.CreateBounded<IOutgoingPacket>( _config.OutgoingPacketsChannelCapacity );
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
        public async Task SendMessageAsync( IOutgoingPacket item )
        {
            var wrapper = new AwaitableOutgoingPacketWrapper( item );
            await _messages.Writer.WriteAsync( wrapper );//ValueTask: most of the time return synchronously
            await wrapper.Sent;//TaskCompletionSource.Task, on some setup will often return synchronously, most of the time, asyncrounously.
        }

        async Task WriteLoop()
        {
            using( _config.OutputLogger?.OutputLoopStarting() )
            {
                try
                {
                    while( !StopToken.IsCancellationRequested )
                    {
                        await _outputProcessor( _config.OutputLogger, ProcessOutgoingPacket, _reflexes, _messages, DisconnectAsync, _processorStopSource.Token );
                    }
                    _pipeWriter.Complete();
                }
                catch( Exception e )
                {
                    _config.OutputLogger?.ExceptionInOutputLoop( e );
                    await DisconnectAsync( DisconnectedReason.UnspecifiedError );
                }
            }
        }

        async ValueTask ProcessOutgoingPacket( IOutputLogger? m, IOutgoingPacket outgoingPacket )
        {
            if( StopToken.IsCancellationRequested ) return;
            using( m?.SendingMessage( ref outgoingPacket, _pconfig.ProtocolLevel ) )
            {
                await outgoingPacket.WriteAsync( _pconfig.ProtocolLevel, _pipeWriter, StopToken );
                if( outgoingPacket is IOutgoingPacketWithId packetWithId ) _packetStore.IdStore.PacketSent( m, packetWithId.PacketId );
            }
        }

        protected override Task OnClosedAsync( Task loop )
        {
            _processorStopSource?.Cancel();
            return loop;
        }

    }
}
