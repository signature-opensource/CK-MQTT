using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static CK.MQTT.IOutgoingPacket;

namespace CK.MQTT
{
    public class AwaitableOutgoingPacketWrapper : IOutgoingPacket
    {
        readonly IOutgoingPacket _outgoingPacket;
        readonly TaskCompletionSource<object?> _taskCompletionSource = new TaskCompletionSource<object?>();
        public AwaitableOutgoingPacketWrapper( IOutgoingPacket outgoingPacket )
        {
            _outgoingPacket = outgoingPacket;
        }

        public Task Sent => _taskCompletionSource.Task;

        public int Size => _outgoingPacket.Size;

        public async ValueTask<WriteResult> WriteAsync( PipeWriter writer, CancellationToken cancellationToken )
        {
            WriteResult res = await _outgoingPacket.WriteAsync( writer, cancellationToken );
            _taskCompletionSource.SetResult( null );
            return res;
        }

        public override string ToString() => $"AwaitableWrapper({_outgoingPacket})";
    }
}
