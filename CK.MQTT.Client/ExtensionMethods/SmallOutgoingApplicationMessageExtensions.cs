using CK.Core;
using System;
using System.Buffers;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public static class SmallOutgoingApplicationMessageExtensions
    {
        public static async ValueTask<Task> PublishAsync( this IMqtt3Client client, IActivityMonitor? m, string topic, QualityOfService qos, bool retain, ReadOnlyMemory<byte> payload )
            => await client.SendPacketAsync<object>( m, new SmallOutgoingApplicationMessage( topic, qos, retain, payload ) );

        public static async ValueTask<Task> PublishAsync( this IMqtt5Client client, IActivityMonitor? m, string topic, QualityOfService qos, bool retain, ReadOnlyMemory<byte> payload,
            string? responseTopic = null, ushort correlationDataSize = 0, SpanAction? correlationDataWriter = null ) //properties
            => await client.SendPacketAsync<object>( m, new SmallOutgoingApplicationMessage( topic, qos, retain, payload, responseTopic, correlationDataSize, correlationDataWriter ) );
    }
}
