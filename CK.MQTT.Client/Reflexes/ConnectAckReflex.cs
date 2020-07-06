using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class ConnectAckReflex
    {
        readonly TaskCompletionSource<ConnectResult> _tcs = new TaskCompletionSource<ConnectResult>();
        readonly Reflex _reflex;
        public ConnectAckReflex( Reflex reflex )
        {
            _reflex = reflex;
        }

        public Task<ConnectResult> Task => _tcs.Task;
        public async ValueTask ProcessIncomingPacket( IMqttLogger m, IncomingMessageHandler sender, byte header, int packetSize, PipeReader reader )
        {
            if( header != (byte)PacketType.ConnectAck )
            {
                _tcs.SetResult( new ConnectResult( ConnectError.ProtocolError ) );
            }
            ReadResult? read = await reader.ReadAsync( m, 2, default );
            if( !read.HasValue ) return;
            ConnectAck.Deserialize( read.Value.Buffer, out byte state, out byte code, out SequencePosition position );
            reader.AdvanceTo( position );
            packetSize -= 2;//TODO: The packet size shouldn't be hardcoded here...
            if( packetSize > 0 )
            {
                await reader.BurnBytes( packetSize );
                m.Warn( "Remaining bytes at the end of the packet. Ignoring them !" );
            }
            sender.CurrentReflex = _reflex;
            _tcs.SetResult( new ConnectResult( (SessionState)state, (ConnectReturnCode)code ) );
        }
    }
}
