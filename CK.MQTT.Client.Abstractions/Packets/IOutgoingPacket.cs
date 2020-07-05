using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IOutgoingPacket
    {
        ValueTask WriteAsync( PipeWriter writer, CancellationToken cancellationToken );

        int Size { get; }

        bool Burned { get; }
    }
}
