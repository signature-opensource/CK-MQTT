using CK.MQTT.Common.Packets;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public abstract class OutgoingPacket
    {
        public abstract ValueTask WriteAsync( PipeWriter writer, CancellationToken cancellationToken );
    }
}
