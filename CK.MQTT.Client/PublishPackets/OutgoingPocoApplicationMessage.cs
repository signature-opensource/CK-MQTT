using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CK.Core.Extension;
using static CK.Core.Extension.PipeReaderExtensions;

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
        public delegate ValueTask ApplicationMessageHandlerDelegate( string topic, Func<Func<ReadOnlyMemory<byte>, ValueTask>, ValueTask> payloadWriter, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken = default );
        public static void SetMessageHandler( this IMqtt3Client client, ApplicationMessageHandlerDelegate messageHandler )
        {
            client.SetMessageHandler( async ( topic, pipeReader, payloadLength, qos, retain, cancellationToken ) =>
             {
                 using( IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent( payloadLength ) )
                 {
                     Memory<byte> buffer = memoryOwner.Memory[..payloadLength];
                     FillStatus res = await pipeReader.FillBuffer( buffer, cancellationToken );
                     if( res != FillStatus.Done ) throw new EndOfStreamException();
                     PipeReader? a;
                     a.
                     //TODO:
                     // Block
                     //     PipeReader in param (default)
                     //     ReadOnlyMemory in async
                     //     ReadOnlySpan in sync
                     // Non blocking:
                     // New and we love the GC.
                     // MemoryPool and disposable message
                 }
             } );
        }
    }
}
