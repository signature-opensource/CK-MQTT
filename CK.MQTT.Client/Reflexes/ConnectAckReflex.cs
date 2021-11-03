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

        /// <summary>
        /// Upon receiving the CONNACK packet, will set <see cref="InputPump.CurrentReflex"/> to this property value.
        /// </summary>
        public Reflex? Reflex { get; set; } // TODO .NET5: Maybe we can use "init" ?

        /// <summary>
        /// <see cref="Task{TResult}"/> that complete when receiving the CONNACK packet.
        /// </summary>
        /// <remarks>
        /// Code awaiting this will run concurrently with <see cref="Reflex"/> delegate.
        /// You should not await this to set the <see cref="InputPump.CurrentReflex"/>. <br/>
        /// Explanation:
        /// The input pump messages in a loop. The mqtt spec allow to send a CONNECTACK and immediatly following retained messages.
        /// This mean while your task will be processed, the <see cref="InputPump"/> will be processing the next message.
        /// </remarks>
        public Task<ConnectResult> Task => _tcs.Task;

        public async ValueTask HandleRequestAsync( IInputLogger? m, InputPump sender, byte header, int packetSize, PipeReader reader, CancellationToken cancellationToken )
        {
            try
            {
                if( Reflex == null ) throw new NullReferenceException( nameof( Reflex ) );
                if( header != (byte)PacketType.ConnectAck )
                {
                    _tcs.SetResult( new ConnectResult( ConnectError.ProtocolError_UnexpectedConnectResponse ) );
                    return;
                }
                using( m?.ProcessPacket( PacketType.ConnectAck ) )
                {
                    ReadResult? read = await reader.ReadAsync( m, 2, cancellationToken );
                    if( !read.HasValue ) return;
                    Deserialize( read.Value.Buffer, out byte state, out byte code, out SequencePosition position );
                    reader.AdvanceTo( position );
                    if( state > 1 )
                    {
                        _tcs.SetResult( new ConnectResult( ConnectError.ProtocolError_InvalidConnackState ) );
                        return;
                    }
                    if( code > 5 )
                    {
                        _tcs.SetResult( new ConnectResult( ConnectError.ProtocolError_UnknownReturnCode ) );
                        return;
                    }
                    if( packetSize > 2 )
                    {
                        await reader.SkipBytesAsync( packetSize - 2 );
                        m?.UnparsedExtraBytes( sender, PacketType.ConnectAck, header, packetSize, packetSize );
                    }
                    sender.CurrentReflex = Reflex;
                    _tcs.SetResult( new ConnectResult( (SessionState)state, (ConnectReturnCode)code ) );
                }
            }
            catch( Exception e )
            {
                m?.ConnectionUnknownException( e );
                _tcs.SetResult( new ConnectResult( ConnectError.InternalException ) );
                return;
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
