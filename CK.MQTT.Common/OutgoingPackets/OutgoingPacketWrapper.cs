using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.OutgoingPackets
{
    public class OutgoingPacketWrapper : OutgoingPacket
    {
        readonly OutgoingPacket _outgoingPacket;
        readonly TaskCompletionSource<object?> _taskCompletionSource = new TaskCompletionSource<object?>();
        public OutgoingPacketWrapper( OutgoingPacket outgoingPacket )
        {
            _outgoingPacket = outgoingPacket;
        }
        public Task Sent => _taskCompletionSource.Task;
        public override async ValueTask WriteAsync( PipeWriter writer, CancellationToken cancellationToken )
        {
            await _outgoingPacket.WriteAsync( writer, cancellationToken );
            _taskCompletionSource.SetResult( null );
        }
    }
}
