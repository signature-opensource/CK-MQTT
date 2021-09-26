using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Client
{
    public class DisposableApplicationMessage : IDisposable
    {
        readonly IDisposable _disposable;

        public DisposableApplicationMessage( string topic, ReadOnlyMemory<byte> payload, QualityOfService qoS, bool retain, IDisposable disposable )
            => (Topic, Payload, QoS, Retain, _disposable) = (topic, payload, qoS, retain, disposable);

        public string Topic { get; }
        public ReadOnlyMemory<byte> Payload { get; }
        public QualityOfService QoS { get; }
        public bool Retain { get; }
        public void Dispose() => _disposable.Dispose();
    }
}
