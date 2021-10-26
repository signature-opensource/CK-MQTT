using CK.Core;
using System;
using System.Buffers;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public static class SimpleOutgoingApplicationMessageExtensions
    {
        public static async ValueTask<Task> PublishAsync( this IMqtt3Client client, IActivityMonitor? m, string topic, QualityOfService qos, bool retain,
            Func<int> getPayloadSize, PayloadWriterDelegate payloadWriter ) //Async required to convert wrapped Task<object> to Task.
        {
            MqttBinaryWriter.ThrowIfInvalidMQTTString( topic );
            return await client.SendPacketAsync<object>( m, new BasicOutgoingApplicationMessage( topic, qos, retain, getPayloadSize, payloadWriter ) );
        }

        public static async ValueTask<Task> PublishAsync( this IMqtt5Client client, IActivityMonitor? m, string topic, QualityOfService qos, bool retain, //publish values
            Func<int> getPayloadSize, PayloadWriterDelegate payloadWriter, //payload
            string? responseTopic = null, ushort correlationDataSize = 0, SpanAction? correlationDataWriter = null ) //properties
        {
            MqttBinaryWriter.ThrowIfInvalidMQTTString( topic );
            return await client.SendPacketAsync<object>( m, new BasicOutgoingApplicationMessage( topic, qos, retain, getPayloadSize, payloadWriter, responseTopic, correlationDataSize, correlationDataWriter ) );
        }
    }
}
