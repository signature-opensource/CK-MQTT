using CK.MQTT.Common.Packets;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public abstract class OutgoingPacket
    {
        protected abstract PacketType PacketType { get; }
        public abstract ValueTask WriteAsync( PipeWriter writer, CancellationToken cancellationToken );
    }
}
