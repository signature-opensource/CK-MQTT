using CK.MQTT.Pumps;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class ConnectAckReflex
    {
        readonly TaskCompletionSource<ConnectResult> _tcs = new();
        readonly Reflex? _reflex;

        /// <summary>
        /// Upon receiving the CONNACK packet, will set <see cref="InputPump.CurrentReflex"/> to this value.
        /// </summary>
        /// <param name="reflex"></param>
        public ConnectAckReflex( Reflex? reflex ) => _reflex = reflex;

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

        public async ValueTask<OperationStatus> HandleRequestAsync( IInputLogger? m, InputPump sender, byte header, uint packetSize, PipeReader reader, CancellationToken cancellationToken )
        {
            try
            {
                if( _reflex == null ) throw new NullReferenceException( nameof( _reflex ) );
                if( header != (byte)PacketType.ConnectAck )
                {
                    _tcs.TrySetResult( new ConnectResult( ConnectError.ProtocolError_UnexpectedConnectResponse ) );
                    return OperationStatus.Done;
                }
                using( m?.ProcessPacket( PacketType.ConnectAck ) )
                {
                    ReadResult read = await reader.ReadAtLeastAsync( 2, cancellationToken );
                    if( read.Buffer.Length < 2 ) return OperationStatus.NeedMoreData;
                    Deserialize( read.Buffer, out byte state, out byte code, out SequencePosition position );
                    reader.AdvanceTo( position );
                    if( state > 1 )
                    {
                        _tcs.TrySetResult( new ConnectResult( ConnectError.ProtocolError_InvalidConnackState ) );
                        return OperationStatus.Done;
                    }
                    if( code > 5 )
                    {
                        _tcs.TrySetResult( new ConnectResult( ConnectError.ProtocolError_UnknownReturnCode ) );
                        return OperationStatus.Done;
                    }
                    if( packetSize > 2 )
                    {
                        await reader.SkipBytesAsync( packetSize - 2, cancellationToken );
                        m?.UnparsedExtraBytes( sender, PacketType.ConnectAck, header, packetSize, packetSize );
                    }
                    sender.CurrentReflex = _reflex;
                    _tcs.TrySetResult( new ConnectResult( (SessionState)state, (ConnectReturnCode)code ) );
                    return OperationStatus.Done;
                }
            }
            catch( Exception e )
            {
                m?.ConnectionUnknownException( e );
                _tcs.SetResult( new ConnectResult( ConnectError.InternalException ) );
                return OperationStatus.Done;
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
