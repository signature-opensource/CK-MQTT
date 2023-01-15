using CK.MQTT.Client;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Pumps
{

    /// <summary>
    /// Message pump that does basic processing on the incoming data
    /// and delegates the message processing job to the <see cref="MQTT.Reflex"/>.
    /// </summary>
    public class InputPump : PumpBase
    {
        readonly IMQTT3Sink _sink;
        readonly PipeReader _pipeReader;

        /// <summary>
        /// Current <see cref="MQTT.Reflex"/> that will be run on the incoming messages.
        /// </summary>
        public Reflex Reflex { get; }

        /// <summary>
        /// Initializes an <see cref="InputPump"/> and immediatly starts to process incoming packets.
        /// </summary>
        /// <param name="pipeReader">The <see cref="PipeReader"/> to read data from.</param>
        /// <param name="reflex">The <see cref="MQTT.Reflex"/> that will process incoming packets.</param>
        public InputPump( IMQTT3Sink sink, PipeReader pipeReader, Func<DisconnectReason, ValueTask> closeHandler, Reflex reflex )
            : base( closeHandler )
        {
            _sink = sink;
            _pipeReader = pipeReader;
            Reflex = reflex;
        }



        protected virtual async ValueTask<ReadResult> ReadAsync( CancellationToken cancellationToken )
            => await _pipeReader.ReadAsync( cancellationToken );

        protected override async Task WorkLoopAsync(CancellationToken stopToken, CancellationToken closeToken)
        {
            try
            {
                while( !stopToken.IsCancellationRequested )
                {
                    var read = await ReadAsync( closeToken);
                    if( stopToken.IsCancellationRequested || read.IsCanceled )
                    {
                        break; // When we are notified to stop, we don't need to notify the external world of it.
                    }
                    //The packet header require 2-5 bytes
                    OperationStatus res = MQTTTHelper.TryParsePacketHeader( read.Buffer, out byte header, out uint length, out SequencePosition position );
                    if( res == OperationStatus.InvalidData )
                    {
                        await _pipeReader.CompleteAsync();
                        await SelfDisconnectAsync( DisconnectReason.ProtocolError );
                        break;
                    }
                    if( res == OperationStatus.Done )
                    {
                        _pipeReader.AdvanceTo( position );
                        OperationStatus status = await Reflex.ProcessIncomingPacketAsync(_sink, this, header, length, _pipeReader, stopToken);
                        if( status == OperationStatus.InvalidData )
                        {
                            await _pipeReader.CompleteAsync();
                            await SelfDisconnectAsync( DisconnectReason.ProtocolError );
                            return;
                        }
                        if( status == OperationStatus.NeedMoreData )
                        {
                            await _pipeReader.CompleteAsync();
                            if( !stopToken.IsCancellationRequested )
                            {
                                //End Of Stream
                                await SelfDisconnectAsync( DisconnectReason.RemoteDisconnected );
                            }
                            return;
                            // TODO: I think only the reading may know the connexion is closed, and should close the client.
                        }
                        continue;
                    }
                    Debug.Assert( res == OperationStatus.NeedMoreData );
                    if( read.IsCompleted )
                    {
                        await _pipeReader.CompleteAsync();
                        await SelfDisconnectAsync( DisconnectReason.RemoteDisconnected );
                        return;
                    }
                    _pipeReader.AdvanceTo( read.Buffer.Start, read.Buffer.End );//Mark data observed, so we will wait new data.
                }
                await _pipeReader.CompleteAsync();

            }
            catch( IOException e )
            {
                await _pipeReader.CompleteAsync( e );
                await SelfDisconnectAsync( DisconnectReason.RemoteDisconnected );
            }
            catch( OperationCanceledException e )
            {
                await _pipeReader.CompleteAsync( e );
            }
            catch( ProtocolViolationException e )
            {
                await _pipeReader.CompleteAsync( e );
                await SelfDisconnectAsync( DisconnectReason.ProtocolError );
            }
            catch( Exception e )
            {
                await _pipeReader.CompleteAsync( e );
                await SelfDisconnectAsync( DisconnectReason.InternalException );
            }
        }
    }
}
