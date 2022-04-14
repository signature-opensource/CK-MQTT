using CK.MQTT.Packets;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.P2P
{
    class FilterUpdatePacket : IOutgoingPacket
    {
        public FilterUpdatePacket( bool subscribe, string[] topics )
        {
            Subscribe = subscribe;
            Topics = topics;
        }

        public ushort PacketId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public QualityOfService Qos => throw new NotImplementedException();

        public bool IsRemoteOwnedPacketId => throw new NotImplementedException();

        public bool Subscribe { get; }
        public string[] Topics { get; }

        public uint GetSize( ProtocolLevel protocolLevel ) => throw new NotImplementedException();

        public ValueTask<WriteResult> WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken ) => throw new NotImplementedException();
    }
}
