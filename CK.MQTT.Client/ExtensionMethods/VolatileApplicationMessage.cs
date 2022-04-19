using System;

namespace CK.MQTT.Client
{
    public class VolatileApplicationMessage : ApplicationMessage, IDisposable
    {
        readonly IDisposable _disposable;

        public VolatileApplicationMessage( string topic, ReadOnlyMemory<byte> payload, QualityOfService qos, bool retain, IDisposable disposable )
            : base( topic, payload, qos, retain )
            => _disposable = disposable;

        public void Dispose() => _disposable.Dispose();
    }
}
