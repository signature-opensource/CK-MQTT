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
        readonly ProtocolConfiguration _pconfig;
        readonly MqttConfigurationBase _config;
        readonly IMqttIdStore _store;
        CancellationTokenSource _processorStopSource = new CancellationTokenSource();

        /// <summary>
        /// Instantiates a new <see cref="OutputPump"/>.
        /// </summary>
        /// <param name="writer">The pipe where the pump will write the messages to.</param>
        /// <param name="store">The packet store to use to retrieve packets.</param>
        public OutputPump( PumppeteerBase pumppeteer, ProtocolConfiguration pconfig, OutputProcessor initialProcessor, PipeWriter writer, IMqttIdStore store )
            : base( pumppeteer )
        {

            (_pipeWriter, _pconfig, _config, _store, _outputProcessor) = (writer, pconfig, pumppeteer.Configuration, store, initialProcessor);
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
        public async Task SendMessageWithoutPacketIdAsync( IOutgoingPacket item )
        {
            var wrapper = new AwaitableOutgoingPacketWrapper( item );
            await _messages.Writer.WriteAsync( wrapper );//ValueTask: most of the time return synchronously
            await wrapper.Sent;//TaskCompletionSource.Task, on some setup will often return synchronously, most of the time, asyncrounously.
        }

        public async Task SendMessageWithPacketIdAsync( IOutgoingPacketWithId item )
        {
            Debug.Assert( item.PacketId != 0 );
            var wrapper = new AwaitableOutgoingPacketWithIdWrapper( item );
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
            if( outgoingPacket is IOutgoingPacketWithId packetWithId )
            {
                using( m?.SendingMessageWithId( ref outgoingPacket, _pconfig.ProtocolLevel, packetWithId.PacketId ) )
                {
                    _store.OnPacketSent( m, packetWithId.PacketId ); // This should be done BEFORE writing the packet.
                    // Explanation:
                    // Sometimes, the receiver and input loop is faster than simply running "SendingPacket".
                    await outgoingPacket.WriteAsync( _pconfig.ProtocolLevel, _pipeWriter, StopToken );
                }
            }
            else
            {
                using( m?.SendingMessage( ref outgoingPacket, _pconfig.ProtocolLevel ) )
                {
                    await outgoingPacket.WriteAsync( _pconfig.ProtocolLevel, _pipeWriter, StopToken );
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
