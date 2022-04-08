using CK.MQTT.Client;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Pumps
{

    public delegate ValueTask<OperationStatus> Reflex( IMqtt3Sink sink, InputPump sender, byte header, uint packetSize, PipeReader reader, CancellationToken cancellationToken );

    /// <summary>
    /// Message pump that does basic processing on the incoming data
    /// and delegates the message processing job to the <see cref="Reflex"/>.
    /// </summary>
    public class InputPump : PumpBase
    {
        readonly Mqtt3ConfigurationBase _config;
        readonly PipeReader _pipeReader;
        readonly IMqtt3Sink _sink;

        /// <summary>
        /// Initializes an <see cref="InputPump"/> and immediatly starts to process incoming packets.
        /// </summary>
        /// <param name="pipeReader">The <see cref="PipeReader"/> to read data from.</param>
        /// <param name="reflex">The <see cref="Reflex"/> that will process incoming packets.</param>
        public InputPump( IMqtt3Sink sink, Func<DisconnectReason, ValueTask> onDisconnect, Mqtt3ConfigurationBase config, PipeReader pipeReader, Reflex reflex ) : base( onDisconnect )
        {
            (_config, _pipeReader, CurrentReflex) = (config, pipeReader, reflex);
            SetRunningLoop( ReadLoopAsync() );
            _sink = sink;
        }

        /// <summary>
        /// Current <see cref="Reflex"/> that will be run on the incoming messages.
        /// </summary>
        public Reflex CurrentReflex { get; set; }

        static OperationStatus TryParsePacketHeader( ReadOnlySequence<byte> sequence, out byte header, out uint length, out SequencePosition position )
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

        protected virtual async ValueTask<ReadResult> ReadAsync( CancellationToken cancellationToken )
            => await _pipeReader.ReadAsync( cancellationToken );

        async Task ReadLoopAsync()
        {
            try
            {
                while( !StopToken.IsCancellationRequested )
                {
                    var read = await ReadAsync( CloseToken );
                    if( CloseToken.IsCancellationRequested || read.IsCanceled )
                    {
                        break; // When we are notified to stop, we don't need to notify the external world of it.
                    }
                    //The packet header require 2-5 bytes
                    OperationStatus res = TryParsePacketHeader( read.Buffer, out byte header, out uint length, out SequencePosition position );
                    if( res == OperationStatus.InvalidData )
                    {
                        await SelfCloseAsync( DisconnectReason.ProtocolError );
                        break;
                    }
                    if( res == OperationStatus.Done )
                    {
                        _pipeReader.AdvanceTo( position );
                        OperationStatus status = await CurrentReflex( _sink, this, header, length, _pipeReader, CloseToken );
                        if( status == OperationStatus.InvalidData )
                        {
                            await SelfCloseAsync( DisconnectReason.ProtocolError );
                            return;
                        }
                        if( status == OperationStatus.NeedMoreData )
                        {
                            if( !CloseToken.IsCancellationRequested )
                            {
                                //End Of Stream
                                await SelfCloseAsync( DisconnectReason.RemoteDisconnected );
                            }
                            return;
                            // TODO: I think only the reading may know the connexion is closed, and should close the client.
                        }
                        continue;
                    }
                    Debug.Assert( res == OperationStatus.NeedMoreData );
                    if( read.IsCompleted )
                    {
                        if( read.Buffer.Length == 0 ) return;
                        await SelfCloseAsync( DisconnectReason.RemoteDisconnected );
                        break;
                    }
                    _pipeReader.AdvanceTo( read.Buffer.Start, read.Buffer.End );//Mark data observed, so we will wait new data.
                }
            }
            catch( OperationCanceledException )
            {
            }
            catch( ProtocolViolationException )
            {
                await SelfCloseAsync( DisconnectReason.ProtocolError );
            }
            catch( Exception )
            {
                await SelfCloseAsync( DisconnectReason.InternalException );
            }
        }


        public override async Task CloseAsync()
        {
            await _pipeReader.CompleteAsync();
            await base.CloseAsync();
        }
    }
}
