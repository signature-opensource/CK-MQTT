using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public delegate ValueTask Reflex( IInputLogger? m, InputPump sender, byte header, int packetSize, PipeReader reader, CancellationToken cancellationToken );

    /// <summary>
    /// Message pump that do basic processing on the incoming data,
    /// and delegate the message processing job to the <see cref="Reflex"/>.
    /// </summary>
    public class InputPump
    {
        readonly MqttConfiguration _config;
        readonly Func<DisconnectedReason, Task> _stopClient;
        readonly PipeReader _pipeReader;
        readonly Task _readLoop;
        readonly CancellationTokenSource _cleanStop = new CancellationTokenSource();
        /// <summary>
        /// Instantiate the <see cref="InputPump"/> and immediatly start to process incoming packets.
        /// </summary>
        /// <param name="stopClient"><see langword="delegate"/> called when the <see cref="InputPump"/> stops.</param>
        /// <param name="pipeReader">The <see cref="PipeReader"/> to read data from.</param>
        /// <param name="reflex">The <see cref="Reflex"/> that will process incoming packets.</param>
        public InputPump( MqttConfiguration config, Func<DisconnectedReason, Task> stopClient, PipeReader pipeReader, Reflex reflex )
        {
            _config = config;
            _stopClient = stopClient;
            _pipeReader = pipeReader;
            CurrentReflex = reflex;
            _readLoop = ReadLoop();
        }

        /// <summary>
        /// Current <see cref="Reflex"/> that will be run on the incoming messages.
        /// </summary>
        public Reflex CurrentReflex { get; set; }

        OperationStatus TryParsePacketHeader( ReadOnlySequence<byte> sequence, out byte header, out int length, out SequencePosition position )
        {
            SequenceReader<byte> reader = new SequenceReader<byte>( sequence );
            length = 0;
            if( !reader.TryRead( out header ) )
            {
                position = reader.Position;
                return OperationStatus.NeedMoreData;
            }
            return reader.TryReadMQTTRemainingLength( out length, out position );
        }

        async Task ReadLoop()
        {
            using( _config.InputLogger?.InputLoopStarting() )
            {
                try
                {
                    while( !_cleanStop.IsCancellationRequested )
                    {
                        ReadResult read = await _pipeReader.ReadAsync( _cleanStop.Token );
                        IInputLogger? m = _config.InputLogger;
                        if( _cleanStop.Token.IsCancellationRequested || read.IsCanceled )
                        {
                            m?.ReadLoopTokenCancelled();
                            break;//The client called the cancel, no need to notify it.
                        }
                        OperationStatus res = TryParsePacketHeader( read.Buffer, out byte header, out int length, out SequencePosition position ); //this guy require 2-5 bytes
                        if( res == OperationStatus.InvalidData )
                        {
                            m?.InvalidIncomingData();
                            _cleanStop.Cancel();
                            await _stopClient( DisconnectedReason.ProtocolError );
                            break;
                        }
                        if( res == OperationStatus.Done )
                        {
                            _pipeReader.AdvanceTo( position );
                            using( m?.IncomingPacket( header, length ) )
                            {
                                await CurrentReflex( m, this, header, length, _pipeReader, _cleanStop.Token );
                            }
                            continue;
                        }
                        Debug.Assert( res == OperationStatus.NeedMoreData );
                        if( read.IsCompleted )
                        {
                            if( read.Buffer.Length == 0 ) m?.EndOfStream();
                            else m?.UnexpectedEndOfStream();
                            _cleanStop.Cancel();
                            await _stopClient( DisconnectedReason.RemoteDisconnected );
                            break;
                        }
                        _pipeReader.AdvanceTo( read.Buffer.Start, read.Buffer.End );//Mark data observed, so we will wait new data.
                    }
                    _pipeReader.Complete();
                    _pipeReader.CancelPendingRead();
                }
                catch(OperationCanceledException e)
                {
                    _config.InputLogger?.LoopCanceledException( e );
                }
                catch( Exception e )
                {
                    _config.InputLogger?.ExceptionOnParsingIncomingData( e );
                    _cleanStop.Cancel();
                    await _stopClient( DisconnectedReason.UnspecifiedError );
                }
            }
        }

        public Task CloseAsync()
        {
            if( _cleanStop.IsCancellationRequested ) return Task.CompletedTask;//Allow to not await ourself.
            _cleanStop.Cancel();
            return _readLoop;
        }
    }
}
