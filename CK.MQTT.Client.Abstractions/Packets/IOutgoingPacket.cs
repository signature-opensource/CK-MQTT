using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IOutgoingPacket
    {
        ValueTask<bool> WriteAsync( PipeWriter writer, CancellationToken cancellationToken );

        int Size { get; }
    }
}
