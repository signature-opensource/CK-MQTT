using System;

namespace CK.MQTT.P2P
{
    public class ConnectAckPacket : SimpleOutgoingPacket
    {
        readonly bool _sessionPresent;
        readonly ConnectReturnCode _connectReturnCode;

        public ConnectAckPacket( bool sessionPresent, ConnectReturnCode connectReturnCode )
        {
            _sessionPresent = sessionPresent;
            _connectReturnCode = connectReturnCode;
        }
        public override ushort PacketId { get => 0; set => throw new NotSupportedException(); }
        public override QualityOfService Qos => QualityOfService.AtMostOnce;

        public override uint GetSize( ProtocolLevel protocolLevel ) => 4;
        public override bool IsRemoteOwnedPacketId => false;

        protected override void Write( ProtocolLevel protocolLevel, Span<byte> buffer )
        {
            buffer[0] = (byte)PacketType.ConnectAck;
            buffer[1] = 2;
            buffer[2] = _sessionPresent ? (byte)1 : (byte)0;
            buffer[3] = (byte)_connectReturnCode;
        }
    }
}
