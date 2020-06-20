using CK.MQTT.Common;
using CK.MQTT.Common.Serialisation;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Abstractions.Packets
{
    public abstract class OutgoingLastWill : IOutgoingPacket
    {
        public bool Dup { get; }
        public bool Retain { get; }
        public QualityOfService Qos { get; }

        readonly string _topic;
        protected OutgoingLastWill(
            bool dup,
            bool retain,
            string topic,
            QualityOfService qos
            )
        {
            Dup = dup;
            Retain = retain;
            Qos = qos;
            _topic = topic;
        }

        public abstract int GetSize();

        protected abstract ValueTask WritePayload( PipeWriter writer, CancellationToken cancellationToken );

        public async ValueTask WriteAsync( PipeWriter writer, CancellationToken cancellationToken )
        {
            int stringSize = _topic.MQTTSize();
            writer.GetSpan( stringSize ).WriteString( _topic );
            writer.Advance( stringSize );
            var res = await writer.FlushAsync( cancellationToken );
            await WritePayload( writer, cancellationToken );
        }
    }
}
