using CK.Core;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static CK.MQTT.IOutgoingPacket;

namespace CK.MQTT
{
    class SmallOutgoingApplicationMessage : OutgoingMessage
    {
        private readonly ReadOnlyMemory<byte> _memory;

        public SmallOutgoingApplicationMessage( string topic, QualityOfService qos, bool retain, ReadOnlyMemory<byte> payload,
            string? responseTopic = null, ushort correlationDataSize = 0, SpanAction? correlationDataWriter = null )//properties
            : base( topic, qos, retain, responseTopic, correlationDataSize, correlationDataWriter )
        {
            _memory = payload;
        }

        protected override int PayloadSize => _memory.Length;

        protected async override ValueTask<WriteResult> WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            await pw.WriteAsync( _memory ).AsNonGenericValueTask();
            return WriteResult.Written;
        }
    }

    public static class SmallOutgoingApplicationMessageExtensions
    {
        public static async ValueTask<Task> PublishAsync( this IMqtt3Client client, IActivityMonitor? m, string topic, QualityOfService qos, bool retain, ReadOnlyMemory<byte> payload )
            => await client.SendPacketAsync<object>( m, new SmallOutgoingApplicationMessage( topic, qos, retain, payload ) );

        public static async ValueTask<Task> PublishAsync( this IMqtt5Client client, IActivityMonitor? m, string topic, QualityOfService qos, bool retain, ReadOnlyMemory<byte> payload,
            string? responseTopic = null, ushort correlationDataSize = 0, SpanAction? correlationDataWriter = null ) //properties
            => await client.SendPacketAsync<object>( m, new SmallOutgoingApplicationMessage( topic, qos, retain, payload, responseTopic, correlationDataSize, correlationDataWriter ) );
    }
}
