using System;
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
        readonly TaskCompletionSource<object?> _taskCompletionSource = new();
        public AwaitableOutgoingPacketWrapper( IOutgoingPacket outgoingPacket )
        {
            _outgoingPacket = outgoingPacket;
        }

        public Task Sent => _taskCompletionSource.Task;

        public QualityOfService Qos => _outgoingPacket.Qos;
        public int PacketId { get => _outgoingPacket.PacketId; set => _outgoingPacket.PacketId = value; }

        public int GetSize( ProtocolLevel protocolLevel ) => _outgoingPacket.GetSize( protocolLevel );

        public async ValueTask<WriteResult> WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken )
        {
            WriteResult res = await _outgoingPacket.WriteAsync( protocolLevel, writer, cancellationToken );
            _taskCompletionSource.SetResult( null );
            return res;
        }

        public override string ToString() => $"AwaitableWrapper({_outgoingPacket})";
    }

    public class AwaitableOutgoingPacketWithIdWrapper : IOutgoingPacket
    {
        readonly IOutgoingPacket _outgoingPacket;
        readonly TaskCompletionSource<object?> _taskCompletionSource = new();
        public AwaitableOutgoingPacketWithIdWrapper( IOutgoingPacket outgoingPacket )
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
