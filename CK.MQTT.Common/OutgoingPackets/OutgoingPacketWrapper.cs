using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
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

        public async ValueTask<bool> WriteAsync( PipeWriter writer, CancellationToken cancellationToken )
        {
            bool burned = await _outgoingPacket.WriteAsync( writer, cancellationToken );
            _taskCompletionSource.SetResult( null );
            return burned;
        }
    }
}
