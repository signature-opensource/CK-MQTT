using CK.MQTT.Packets;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server.OutgoingPackets;

class InternalUnsubscribePacket : IOutgoingPacket
{
    public InternalUnsubscribePacket( string[] topics )
    {
        Topics = topics;
    }

    public ushort PacketId { get => 0; set => throw new NotSupportedException(); }

    public QualityOfService Qos => throw new NotSupportedException();

    public bool IsRemoteOwnedPacketId => throw new NotSupportedException();

    public string[] Topics { get; }

    public PacketType Type => PacketType.Unsubscribe;

    public uint GetSize( ProtocolLevel protocolLevel ) => throw new NotSupportedException();

    public ValueTask WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken ) => throw new NotSupportedException();
}
