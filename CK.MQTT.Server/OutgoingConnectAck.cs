using CK.MQTT.Packets;
using System;

namespace CK.MQTT.Server;

public class OutgoingConnectAck : SimpleOutgoingPacket
{
    readonly bool _sessionPresent;
    readonly ProtocolConnectReturnCode _connectReturnCode;

    public OutgoingConnectAck( bool sessionPresent, ProtocolConnectReturnCode connectReturnCode )
    {
        _sessionPresent = sessionPresent;
        _connectReturnCode = connectReturnCode;
    }
    public override ushort PacketId { get => 0; set => throw new NotSupportedException(); }
    public override QualityOfService Qos => QualityOfService.AtMostOnce;

    public override uint GetSize( ProtocolLevel protocolLevel ) => 4;
    public override bool IsRemoteOwnedPacketId => false;

    public override PacketType Type => PacketType.ConnectAck;

    protected override void Write( ProtocolLevel protocolLevel, Span<byte> buffer )
    {
        buffer[0] = (byte)PacketType.ConnectAck;
        buffer[1] = 2;
        buffer[2] = _sessionPresent ? (byte)1 : (byte)0;
        buffer[3] = (byte)_connectReturnCode;
    }
}
