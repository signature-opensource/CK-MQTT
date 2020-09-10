using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using CK.Core.Extension;

namespace CK.MQTT.Common.PublishPackets
{
    public class ApplicationMessage
    {
        public ApplicationMessage( string topic, ReadOnlyMemory<byte> payload, QualityOfService qoS, bool retain )
        {
            Topic = topic;
            Payload = payload;
            QoS = qoS;
            Retain = retain;
        }
        public string Topic { get; }
        public ReadOnlyMemory<byte> Payload { get; }
        public QualityOfService QoS { get; }
        public bool Retain { get; }
    }

    public static class MqttApplicationMessageExtensions
    {
        public static void SetMessageHandler( this IMqtt3Client client, Func<ApplicationMessage, ValueTask> handler )
        {
            client.SetMessageHandler( async ( topic, pipeReader, payloadLengt, qos, retain ) =>
             {
             } );
        }
    }
}
