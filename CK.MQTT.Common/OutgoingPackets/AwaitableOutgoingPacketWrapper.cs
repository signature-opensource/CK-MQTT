using System.Diagnostics;
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

        public int GetSize( ProtocolLevel protocolLevel ) => _outgoingPacket.GetSize( protocolLevel );

        public async ValueTask<WriteResult> WriteAsync( ProtocolLevel protocolLevel , PipeWriter writer, CancellationToken cancellationToken )
        {
            WriteResult res = await _outgoingPacket.WriteAsync( protocolLevel, writer, cancellationToken );
            _taskCompletionSource.SetResult( null );
            return res;
        }

        public override string ToString() => $"AwaitableWrapper({_outgoingPacket})";
    }

    public class AwaitableOutgoingPacketWithIdWrapper : IOutgoingPacketWithId
    {
        readonly IOutgoingPacketWithId _outgoingPacket;
        readonly TaskCompletionSource<object?> _taskCompletionSource = new TaskCompletionSource<object?>();
        public AwaitableOutgoingPacketWithIdWrapper( IOutgoingPacketWithId outgoingPacket )
        {
            Debug.Assert( outgoingPacket.PacketId != 0 );
            _outgoingPacket = outgoingPacket;
        }

        public Task Sent => _taskCompletionSource.Task;
        public int PacketId { get => _outgoingPacket.PacketId; set => _outgoingPacket.PacketId = value; }
        public QualityOfService Qos => _outgoingPacket.Qos;
        public int GetSize( ProtocolLevel protocolLevel ) => _outgoingPacket.GetSize( protocolLevel );

        public async ValueTask<WriteResult> WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken )
        {
            WriteResult res = await _outgoingPacket.WriteAsync( protocolLevel, writer, cancellationToken );
            _taskCompletionSource.SetResult( null );
            return res;
        }

        public override string ToString() => $"AwaitableWrapper({_outgoingPacket})";
    }
}
