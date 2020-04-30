using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Channels
{
    class GenericChannelStream : IGenericChannelStream
    {
        readonly IStreamClient _client;
        readonly Stream _stream;
        public GenericChannelStream( IStreamClient client, Stream stream )
        {
            _client = client;
            _stream = stream;
        }
        public bool IsConnected => _client.IsConnected;

        public void Close() => _client.Close();

        public void Dispose() => _client.Dispose();

        public ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken ) =>
             _stream.ReadAsync( buffer, cancellationToken );

        public ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken ) =>
            _stream.WriteAsync( buffer, cancellationToken );
    }
}
