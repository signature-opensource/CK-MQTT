using CK.MQTT.Client;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Pumps
{

    /// <summary>
    /// Message pump that does basic processing on the incoming data
    /// and delegates the message processing job to the <see cref="Reflex"/>.
    /// </summary>
    public class InputPump : PumpBase
    {
        /// <summary>
        /// Initializes an <see cref="InputPump"/> and immediatly starts to process incoming packets.
        /// </summary>
        /// <param name="pipeReader">The <see cref="PipeReader"/> to read data from.</param>
        /// <param name="reflex">The <see cref="Reflex"/> that will process incoming packets.</param>
        public InputPump( MessageExchanger messageExchanger, Reflex reflex ) : base( messageExchanger )
        {
            CurrentReflex = reflex;
        }

        /// <summary>
        /// Current <see cref="Reflex"/> that will be run on the incoming messages.
        /// </summary>
        public Reflex CurrentReflex { get; set; }

        public static OperationStatus TryParsePacketHeader( ReadOnlySequence<byte> sequence, out byte header, out uint length, out SequencePosition position )
        {
            SequenceReader<byte> reader = new( sequence );
            length = 0;
            if( !reader.TryRead( out header ) )
            {
                position = reader.Position;
                return OperationStatus.NeedMoreData;
            }
            var res = reader.TryReadVariableByteInteger( out length );
            position = reader.Position;
            return res;
        }

        protected virtual async ValueTask<ReadResult> ReadAsync( CancellationToken cancellationToken )
            => await MessageExchanger.Channel.DuplexPipe!.Input.ReadAsync( cancellationToken );

        public void StartPumping() => SetRunningLoop( WorkLoopAsync() );

        async Task WorkLoopAsync()
        {
            var pipeReader = MessageExchanger.Channel.DuplexPipe!.Input;
            try
            {
                while( !MessageExchanger.StopTokenSource.IsCancellationRequested )
                {
                    var read = await ReadAsync( MessageExchanger.StopTokenSource.Token );
                    if( MessageExchanger.StopTokenSource.IsCancellationRequested || read.IsCanceled )
                    {
                        break; // When we are notified to stop, we don't need to notify the external world of it.
                    }
                    //The packet header require 2-5 bytes
                    OperationStatus res = TryParsePacketHeader( read.Buffer, out byte header, out uint length, out SequencePosition position );
                    if( res == OperationStatus.InvalidData )
                    {
                        await pipeReader.CompleteAsync();
                        await SelfCloseAsync( DisconnectReason.ProtocolError );
                        break;
                    }
                    if( res == OperationStatus.Done )
                    {
                        pipeReader.AdvanceTo( position );
                        OperationStatus status = await CurrentReflex.ProcessIncomingPacketAsync( MessageExchanger.Sink, this, header, length, pipeReader, MessageExchanger.StopTokenSource.Token );
                        if( status == OperationStatus.InvalidData )
                        {
                            await pipeReader.CompleteAsync();
                            await SelfCloseAsync( DisconnectReason.ProtocolError );
                            return;
                        }
                        if( status == OperationStatus.NeedMoreData )
                        {
                            await pipeReader.CompleteAsync();
                            if( !MessageExchanger.StopTokenSource.IsCancellationRequested )
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
                        await pipeReader.CompleteAsync();
                        await SelfCloseAsync( DisconnectReason.RemoteDisconnected );
                        return;
                    }
                    pipeReader.AdvanceTo( read.Buffer.Start, read.Buffer.End );//Mark data observed, so we will wait new data.
                }
                await pipeReader.CompleteAsync();

            }
            catch( IOException e)
            {
                await pipeReader.CompleteAsync(e);
                await SelfCloseAsync( DisconnectReason.RemoteDisconnected );
            }
            catch( OperationCanceledException e)
            {
                await pipeReader.CompleteAsync(e);
            }
            catch( ProtocolViolationException e)
            {
                await pipeReader.CompleteAsync(e);
                await SelfCloseAsync( DisconnectReason.ProtocolError );
            }
            catch( Exception e)
            {
                await pipeReader.CompleteAsync(e);
                await SelfCloseAsync( DisconnectReason.InternalException );
            }
        }
    }
}
