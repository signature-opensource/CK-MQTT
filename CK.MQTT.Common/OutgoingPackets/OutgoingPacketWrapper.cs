using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public class OutgoingPacketWrapper : IOutgoingPacket
    {
        readonly IOutgoingPacket _outgoingPacket;
        readonly TaskCompletionSource<object?> _taskCompletionSource = new TaskCompletionSource<object?>();
        public OutgoingPacketWrapper( IOutgoingPacket outgoingPacket )
        {
            _outgoingPacket = outgoingPacket;
        }
        public Task Sent => _taskCompletionSource.Task;

        public int Size => _outgoingPacket.Size;

        public bool Burned => _outgoingPacket.Burned;

        public async ValueTask WriteAsync( PipeWriter writer, CancellationToken cancellationToken )
        {
            await _outgoingPacket.WriteAsync( writer, cancellationToken );
            _taskCompletionSource.SetResult( null );
        }
    }
}
