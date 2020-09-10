using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CK.MQTT.IOutgoingPacket;

namespace CK.MQTT
{
    /// <summary>
    /// The message pump that serialize the messages to the <see cref="PipeWriter"/>.
    /// Accept messages concurrently, but it will send them one per one.
    /// </summary>
    public class OutputPump
    {
        public delegate ValueTask OutputProcessor( IOutputLogger? m, PacketSender packetSender, Channel<IOutgoingPacket> reflexes, Channel<IOutgoingPacket> messages, CancellationToken cancellationToken, Func<DisconnectedReason, Task> _clientClose );
        public delegate ValueTask PacketSender( IOutputLogger? m, IOutgoingPacket outgoingPacket );
        readonly Channel<IOutgoingPacket> _messages;
        readonly Channel<IOutgoingPacket> _reflexes;
        OutputProcessor _outputProcessor;
        readonly Func<DisconnectedReason, Task> _clientClose;
        readonly PipeWriter _pipeWriter;
        readonly ProtocolConfiguration _pconfig;
        readonly MqttConfiguration _config;
        readonly PacketStore _packetStore;
        readonly Task _writeLoopTask;
        readonly CancellationTokenSource _stopSource;
        CancellationTokenSource _processorStopSource;
        /// <summary>
        /// Instantiate a new <see cref="OutputPump"/>.
        /// </summary>
        /// <param name="clientClose">A <see langword="delegate"/> that will be called when the pump close.</param>
        /// <param name="writer">The pipe where the pump will write the messages to.</param>
        /// <param name="config">The config to use.</param>
        /// <param name="packetStore">The packet store to use to retrieve packets.</param>
        public OutputPump( OutputProcessor outputProcessor, Func<DisconnectedReason, Task> clientClose, PipeWriter writer, ProtocolConfiguration pconfig, MqttConfiguration config, PacketStore packetStore )
        {
            _messages = Channel.CreateBounded<IOutgoingPacket>( config.ChannelsPacketCount );
            _reflexes = Channel.CreateBounded<IOutgoingPacket>( config.ChannelsPacketCount );
            _clientClose = clientClose;
            _pipeWriter = writer;
            _pconfig = pconfig;
            _config = config;
            _packetStore = packetStore;
            _stopSource = new CancellationTokenSource();
            SetOutputProcessor( outputProcessor );
            _writeLoopTask = WriteLoop();
        }

        [MemberNotNull( nameof( _processorStopSource ) )]
        [MemberNotNull( nameof( _outputProcessor ) )]
        public void SetOutputProcessor( OutputProcessor outputProcessor )
        {
            _processorStopSource?.Cancel();
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
                    while( !_stopSource.IsCancellationRequested )
                    {
                        await _outputProcessor( _config.OutputLogger, ProcessOutgoingPacket, _reflexes, _messages, _processorStopSource.Token, _clientClose );
                    }
                    _pipeWriter.Complete();
                }
                catch( Exception e )
                {
                    _config.OutputLogger?.ExceptionInOutputLoop( e );
                    _stopSource.Cancel();
                    await _clientClose( DisconnectedReason.UnspecifiedError );
                }
            }
        }

        async ValueTask ProcessOutgoingPacket( IOutputLogger? m, IOutgoingPacket outgoingPacket )
        {
            if( _stopSource.IsCancellationRequested ) return;
            using( m?.SendingMessage( ref outgoingPacket, _pconfig.ProtocolLevel ) )
            {
                await outgoingPacket.WriteAsync( _pconfig.ProtocolLevel, _pipeWriter, _stopSource.Token );
                if( outgoingPacket is IOutgoingPacketWithId packetWithId ) _packetStore.IdStore.PacketSent( m, packetWithId.PacketId );
            }
        }

        public Task CloseAsync()
        {
            if( _stopSource.IsCancellationRequested ) return Task.CompletedTask;//Allow to not await ourself.
            _stopSource.Cancel();
            _processorStopSource?.Cancel();
            return _writeLoopTask;
        }
    }
}
