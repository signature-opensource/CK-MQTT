using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class ConnectAckReflex
    {
        readonly TaskCompletionSource<ConnectResult> _tcs = new TaskCompletionSource<ConnectResult>();
        public Reflex? Reflex { get; set; }
        public Task<ConnectResult> Task => _tcs.Task;
        public async ValueTask ProcessIncomingPacket( IInputLogger? m, IncomingMessageHandler sender, byte header, int packetSize, PipeReader reader )
        {
            if( Reflex == null ) throw new NullReferenceException( nameof( Reflex ) );
            if( header != (byte)PacketType.ConnectAck )
            {
                _tcs.SetResult( new ConnectResult( ConnectError.ProtocolError ) );
            }
            m?.ProcessPacket( PacketType.ConnectAck );
            ReadResult? read = await reader.ReadAsync( m, 2, default );
            if( !read.HasValue ) return;
            ConnectAck.Deserialize( read.Value.Buffer, out byte state, out byte code, out SequencePosition position );
            reader.AdvanceTo( position );
            packetSize -= 2;//TODO: The packet size shouldn't be hardcoded here...
            if( packetSize > 0 )
            {
                await reader.BurnBytes( packetSize );
                m?.UnparsedExtraBytes( sender, PacketType.ConnectAck, header, packetSize, packetSize );
            }
            sender.CurrentReflex = Reflex;
            _tcs.SetResult( new ConnectResult( (SessionState)state, (ConnectReturnCode)code ) );
        }
    }
}
