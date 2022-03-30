using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT.Common.BasicImplementations
{
    public class StreamChannel : IMqttChannel
    {
        readonly IDisposable _disposable;

        public StreamChannel( Stream stream )
        {
            _disposable = stream;
            DuplexPipe = new DuplexPipe(
                PipeReader.Create( stream ),
                PipeWriter.Create( stream )
            );
        }

        class ChainedDispose : IDisposable
        {
            readonly IDisposable _a;
            readonly IDisposable _b;

            public ChainedDispose( IDisposable a, IDisposable b )
            {
                _a = a;
                _b = b;
            }

            public void Dispose()
            {
                _a.Dispose();
                _b.Dispose();
            }
        }

        public StreamChannel( Stream readStream, Stream writeStream )
        {
            _disposable = new ChainedDispose( readStream, writeStream );
            DuplexPipe = new DuplexPipe(
                PipeReader.Create( readStream ),
                PipeWriter.Create( writeStream )
            );
        }

        public bool IsConnected { get; private set; }

        public IDuplexPipe DuplexPipe { get; private set; }

        public void Close()
        {
            IsConnected = false;
            DuplexPipe.Input.Complete();
            DuplexPipe.Output.Complete();
        }

        public void Dispose() => _disposable.Dispose();

        public ValueTask StartAsync() => new();
    }
}
