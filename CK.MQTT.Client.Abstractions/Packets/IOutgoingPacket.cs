using CK.MQTT.Common.Packets;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public interface IOutgoingPacket
    {
        ValueTask WriteAsync( PipeWriter writer, CancellationToken cancellationToken );

        int GetSize();
    }
}
