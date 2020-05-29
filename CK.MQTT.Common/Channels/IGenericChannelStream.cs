using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Channels
{
    public interface IGenericChannelStream : IDisposable
    {
        bool IsConnected { get; }

        public PipeReader Pipe { get; }

        public ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken );
    }
}
