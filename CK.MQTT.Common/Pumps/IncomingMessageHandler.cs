using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public delegate ValueTask Reflex( IMqttLogger m, IncomingMessageHandler sender, byte header, int packetSize, PipeReader reader );

    /// <summary>
    /// Message pump that do basic processing on the incoming data,
    /// and delegate the message processing job to the <see cref="Reflex"/>.
    /// </summary>
    public class IncomingMessageHandler
    {
        readonly MqttConfiguration _config;
        readonly Func<IMqttLogger, DisconnectedReason, Task> _stopClient;
        readonly PipeReader _pipeReader;
        readonly Task _readLoop;
        readonly CancellationTokenSource _cleanStop = new CancellationTokenSource();
        Action<IMqttLogger>? _timeoutLogger;
        /// <summary>
        /// Instantiate the <see cref="IncomingMessageHandler"/> and immediatly start to process incoming packets.
        /// </summary>
        /// <param name="stopClient"><see langword="delegate"/> called when the <see cref="IncomingMessageHandler"/> stops.</param>
        /// <param name="pipeReader">The <see cref="PipeReader"/> to read data from.</param>
        /// <param name="reflex">The <see cref="Reflex"/> that will process incoming packets.</param>
        public IncomingMessageHandler( MqttConfiguration config, Func<IMqttLogger, DisconnectedReason, Task> stopClient, PipeReader pipeReader, Reflex reflex )
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
            using( _config.InputLogger.OpenInfo( "Listening Incoming Messages..." ) )
            {
                try
                {
                    while( !_cleanStop.IsCancellationRequested )
                    {
                        ReadResult read = await _pipeReader.ReadAsync( _cleanStop.Token );
                        IMqttLogger m = _config.InputLogger;
                        if( read.IsCanceled )
                        {
                            m.Trace( "Read Loop Cancelled." );
                            break;//The client called the cancel, no need to notify it.
                        }
                        OperationStatus res = TryParsePacketHeader( read.Buffer, out byte header, out int length, out SequencePosition position ); //this guy require 2-5 bytes
                        if( res == OperationStatus.InvalidData )
                        {
                            m.Error( "Corrupted Stream." );
                            _cleanStop.Cancel();
                            await _stopClient( m, DisconnectedReason.ProtocolError );
                            break;
                        }
                        if( res == OperationStatus.Done )
                        {
                            _pipeReader.AdvanceTo( position );
                            using( m.OpenTrace( $"Incoming packet of {length} bytes." ) )
                            {
                                await CurrentReflex( m, this, header, length, _pipeReader );
                            }
                            continue;
                        }
                        Debug.Assert( res == OperationStatus.NeedMoreData );
                        if( read.IsCompleted )
                        {
                            if( read.Buffer.Length == 0 ) m.Info( "Remote closed channel." );
                            else m.Error( "Unexpected End Of Stream." );
                            _cleanStop.Cancel();
                            await _stopClient( m, DisconnectedReason.RemoteDisconnected );
                            break;
                        }
                        _pipeReader.AdvanceTo( read.Buffer.Start, read.Buffer.End );//Mark data observed, so we will wait new data.
                    }
                    if( _timeoutLogger != null )
                    {
                        using( _config.InputLogger.OpenError( $"A reflex waited a packet too long and timeouted." ) )
                        {
                            _timeoutLogger( _config.InputLogger );
                            await _stopClient( _config.InputLogger, DisconnectedReason.SelfDisconnected );
                        }
                    }
                    _pipeReader.Complete();
                    _pipeReader.CancelPendingRead();
                }
                catch( Exception e )
                {
                    _config.InputLogger.Error( "Error while processing incoming data.", e );
                    _cleanStop.Cancel();
                    await _stopClient( _config.InputLogger, DisconnectedReason.UnspecifiedError );
                }
            }
        }

        /// <summary>
        /// Allow <see cref="Reflex"/> to notify they waited too much a packet and timeouted.
        /// </summary>
        public void SetTimeout( Action<IMqttLogger> timeoutLogger )
        {
            _timeoutLogger = timeoutLogger;
            _cleanStop.Cancel();
        }

        public Task CloseAsync()
        {
            if( _cleanStop.IsCancellationRequested ) return Task.CompletedTask;//Allow to not await ourself.
            _cleanStop.Cancel();
            return _readLoop;
        }
    }
}
