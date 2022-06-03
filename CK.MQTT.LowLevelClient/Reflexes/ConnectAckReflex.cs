using CK.MQTT.Client;
using CK.MQTT.Pumps;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class ConnectAckReflex : IReflexMiddleware
    {
        readonly TaskCompletionSource<ConnectResult> _tcs = new();
        readonly Reflex _reflex;

        /// <summary>
        /// Upon receiving the CONNACK packet, will set <see cref="InputPump.CurrentReflex"/> to this value.
        /// </summary>
        /// <param name="reflex"></param>
        public ConnectAckReflex( MessageExchanger messageExchanger, Reflex reflex )
        {
            _reflex = reflex;
            var builder = new ReflexMiddlewareBuilder();
            builder.UseMiddleware( this );
            AsReflex = builder.Build();
        }

        public Reflex AsReflex { get; }

        /// <summary>
        /// <see cref="Task{TResult}"/> that complete when receiving the CONNACK packet.
        /// </summary>
        /// <remarks>
        /// Code awaiting this will run concurrently with <see cref="_reflex"/> delegate.
        /// You should not await this to set the <see cref="InputPump.CurrentReflex"/>. <br/>
        /// Explanation:
        /// The input pump messages in a loop. The mqtt spec allow to send a CONNECTACK and immediately following retained messages.
        /// This mean while your task will be processed, the <see cref="InputPump"/> will be processing the next message.
        /// </remarks>
        public Task<ConnectResult> Task => _tcs.Task;

        public async ValueTask<(OperationStatus, bool)> ProcessIncomingPacketAsync( IMqtt3Sink sink, InputPump sender, byte header, uint packetSize, PipeReader reader, CancellationToken cancellationToken )
        {
            try
            {
                if( _reflex == null ) throw new NullReferenceException( nameof( _reflex ) );
                if( header != (byte)PacketType.ConnectAck )
                {
                    _tcs.TrySetResult( new ConnectResult( ConnectError.ProtocolError_UnexpectedConnectResponse ) );
                    return (OperationStatus.Done, true);
                }
                ReadResult read = await reader.ReadAtLeastAsync( 2, cancellationToken );
                if( read.Buffer.Length < 2 ) return (OperationStatus.NeedMoreData, true);
                Deserialize( read.Buffer, out byte state, out byte code, out SequencePosition position );
                reader.AdvanceTo( position );
                if( state > 1 )
                {
                    _tcs.TrySetResult( new ConnectResult( ConnectError.ProtocolError_InvalidConnackState ) );
                    return (OperationStatus.Done, true);
                }
                if( code > 5 )
                {
                    _tcs.TrySetResult( new ConnectResult( ConnectError.ProtocolError_UnknownReturnCode ) );
                    return (OperationStatus.Done, true);
                }
                if( packetSize > 2 )
                {
                    await reader.SkipBytesAsync( sink, 0, (ushort)(packetSize - 2), cancellationToken );
                }
                sender.CurrentReflex = _reflex;
                _tcs.TrySetResult( new ConnectResult( (SessionState)state, (ProtocolConnectReturnCode)code ) );
                return (OperationStatus.Done, true);
            }
            catch( EndOfStreamException )
            {
                _tcs.SetResult( new ConnectResult( ConnectError.ProtocolError_IncompleteResponse ) );
                return (OperationStatus.Done, true);
            }
            catch( Exception )
            {
                _tcs.SetResult( new ConnectResult( ConnectError.InternalException ) );
                return (OperationStatus.Done, true);
            }
        }

        public void TrySetCanceled( CancellationToken cancellationToken ) => _tcs.TrySetCanceled( cancellationToken );

        static void Deserialize( ReadOnlySequence<byte> buffer, out byte state, out byte code, out SequencePosition position )
        {
            SequenceReader<byte> reader = new( buffer );
            bool res = reader.TryRead( out state );
            bool res2 = reader.TryRead( out code );
            position = reader.Position;
            Debug.Assert( res && res2 );
        }
    }
}
