using CK.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Channels
{
    public interface IGenericChannelStream : IDisposable
    {
        bool IsConnected { get; }

        public void Close();

        public ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken );

        public ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken );
    }
}
