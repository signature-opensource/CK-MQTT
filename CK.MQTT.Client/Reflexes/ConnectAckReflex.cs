using CK.Core;
using CK.MQTT.Client.Deserialization;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using CK.MQTT.Abstractions.Serialisation;

namespace CK.MQTT.Client.Reflexes
{
    class ConnectAckReflex
    {
        readonly TaskCompletionSource<ConnectResult> _tcs = new TaskCompletionSource<ConnectResult>();
        readonly Reflex _reflex;
        public ConnectAckReflex(Reflex reflex)
        {
            _reflex = reflex;
        }
        

        public Task<ConnectResult> Task => _tcs.Task;
        public async ValueTask ProcessIncomingPacket( IActivityMonitor m, IncomingMessageHandler sender, byte header, int packetSize, PipeReader reader )
        {
            if( header != (byte)PacketType.ConnectAck )
            {
                _tcs.SetResult( new ConnectResult( ConnectError.ProtocolError, SessionState.Unknown, ConnectReturnCode.Unknown ) );
            }
            ReadResult? read = await reader.ReadAsync( m, 2, default );
            if( !read.HasValue ) return;
            ConnectAck.Deserialize( m, read.Value.Buffer, out byte state, out byte code, out SequencePosition position );
            reader.AdvanceTo( position );
            packetSize -= 2;
            if( packetSize > 0 )
            {
                await reader.BurnBytes( packetSize );
                m.Warn( "Remaining bytes at the end of the packet. Ignoring them !" );
            }
            sender.CurrentReflex = _reflex;
            _tcs.SetResult( new ConnectResult( ConnectError.Ok, (SessionState)state, (ConnectReturnCode)code ) );
        }
    }
}
