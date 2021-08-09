using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Pumps
{
    public delegate ValueTask Reflex( IInputLogger? m, InputPump sender, byte header, int packetSize, PipeReader reader, CancellationToken cancellationToken );

    /// <summary>
    /// Message pump that does basic processing on the incoming data
    /// and delegates the message processing job to the <see cref="Reflex"/>.
    /// </summary>
    public class InputPump : PumpBase
    {
        readonly MqttConfigurationBase _config;
        readonly PipeReader _pipeReader;

        /// <summary>
        /// Initializes an <see cref="InputPump"/> and immediatly starts to process incoming packets.
        /// </summary>
        /// <param name="pipeReader">The <see cref="PipeReader"/> to read data from.</param>
        /// <param name="reflex">The <see cref="Reflex"/> that will process incoming packets.</param>
        public InputPump( Func<DisconnectedReason, ValueTask> onDisconnect, MqttConfigurationBase config, PipeReader pipeReader, Reflex reflex ) : base( onDisconnect )
        {
            (_config, _pipeReader, CurrentReflex) = (config, pipeReader, reflex);
            SetRunningLoop( ReadLoopAsync() );
        }

        /// <summary>
        /// Current <see cref="Reflex"/> that will be run on the incoming messages.
        /// </summary>
        public Reflex CurrentReflex { get; set; }

        static OperationStatus TryParsePacketHeader( ReadOnlySequence<byte> sequence, out byte header, out int length, out SequencePosition position )
        {
            SequenceReader<byte> reader = new( sequence );
            length = 0;
            if( !reader.TryRead( out header ) )
            {
                position = reader.Position;
                return OperationStatus.NeedMoreData;
            }
            return reader.TryReadMQTTRemainingLength( out length, out position );
        }

        async Task ReadLoopAsync()
        {
            using( _config.InputLogger?.InputLoopStarting() )
            {
                try
                {
                    while( !StopToken.IsCancellationRequested )
                    {
                        ReadResult read = await _pipeReader.ReadAsync( CloseToken );
                        IInputLogger? m = _config.InputLogger;
                        if( CloseToken.IsCancellationRequested || read.IsCanceled )
                        {
                            m?.ReadLoopTokenCancelled();
                            break; // When we are notified to stop, we don't need to notify the external world of it.
                        }
                        //The packet header require 2-5 bytes
                        OperationStatus res = TryParsePacketHeader( read.Buffer, out byte header, out int length, out SequencePosition position );
                        if( res == OperationStatus.InvalidData )
                        {
                            m?.InvalidIncomingData();
                            await SelfCloseAsync( DisconnectedReason.ProtocolError );
                            break;
                        }
                        if( res == OperationStatus.Done )
                        {
                            _pipeReader.AdvanceTo( position );
                            using( m?.IncomingPacket( header, length ) )
                            {
                                await CurrentReflex( m, this, header, length, _pipeReader, CloseToken );
                            }
                            continue;
                        }
                        Debug.Assert( res == OperationStatus.NeedMoreData );
                        if( read.IsCompleted )
                        {
                            if( read.Buffer.Length == 0 ) m?.EndOfStream();
                            else m?.UnexpectedEndOfStream();
                            await SelfCloseAsync( DisconnectedReason.RemoteDisconnected );
                            break;
                        }
                        _pipeReader.AdvanceTo( read.Buffer.Start, read.Buffer.End );//Mark data observed, so we will wait new data.
                    }
                }
                catch( OperationCanceledException e )
                {
                    _config.InputLogger?.LoopCanceledException( e );
                }
                catch( Exception e )
                {
                    _config.InputLogger?.ExceptionOnParsingIncomingData( e );
                    await SelfCloseAsync( DisconnectedReason.InternalException );
                }
            }
        }


        public override Task CloseAsync()
        {
            _pipeReader.CompleteAsync();
            return base.CloseAsync();
        }
    }
}
