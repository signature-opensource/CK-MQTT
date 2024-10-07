using CK.MQTT.Packets;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server.OutgoingPackets;

class InternalSubscribePacket : IOutgoingPacket
{
    public InternalSubscribePacket( Subscription[] topics )
    {
        Topics = topics;
    }

    public ushort PacketId { get => 0; set => throw new NotSupportedException(); }

    public QualityOfService Qos => throw new NotSupportedException();

    public bool IsRemoteOwnedPacketId => throw new NotSupportedException();

    public Subscription[] Topics { get; }

    public PacketType Type => PacketType.Subscribe;

    public uint GetSize( ProtocolLevel protocolLevel ) => throw new NotSupportedException();

    public ValueTask WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken ) => throw new NotImplementedException();
}
