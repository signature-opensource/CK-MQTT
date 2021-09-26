using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Common.BasicImplementations
{
    public class StreamChannel : IMqttChannel
    {
        readonly Stream _stream;

        public StreamChannel( Stream stream )
        {
            _stream = stream;
            DuplexPipe = new DuplexPipe(
                PipeReader.Create( _stream ),
                PipeWriter.Create( _stream )
            );
        }
        public bool IsConnected { get; private set; }

        public IDuplexPipe DuplexPipe { get; private set; }

        public void Close( IInputLogger? m )
        {
            IsConnected = false;
            DuplexPipe.Input.Complete();
            DuplexPipe.Output.Complete();
        }

        public void Dispose() => _stream.Dispose();

        public ValueTask StartAsync( IActivityMonitor? m ) => new();
    }
}
