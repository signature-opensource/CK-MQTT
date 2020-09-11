using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static CK.Core.Extension.PipeReaderExtensions;

namespace CK.MQTT.Client.HandlerExtensions
{
    public static class BlockingHandlerExtensions
    {
        public delegate ValueTask MemoryMessageHandlerDelegate( string topic, ReadOnlyMemory<byte> buffer, QualityOfService qos, bool retain, CancellationToken cancellationToken = default );
        public static void SetMessageHandler( this IMqtt3Client client, MemoryMessageHandlerDelegate messageHandler )
        {
            client.SetMessageHandler( async ( topic, pipeReader, payloadLength, qos, retain, cancellationToken ) =>
            {
                using( IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent( payloadLength ) )
                {
                    Memory<byte> buffer = memoryOwner.Memory[..payloadLength];
                    FillStatus res = await pipeReader.FillBuffer( buffer, cancellationToken );
                    if( res != FillStatus.Done ) throw new EndOfStreamException();
                    await messageHandler( topic, buffer, qos, retain, cancellationToken );
                }
            } );
        }
        public delegate void SpanMessageHandlerDelegate( string topic, ReadOnlySpan<byte> buffer, QualityOfService qos, bool retain );
        public static void SetMessageHandler( this IMqtt3Client client, SpanMessageHandlerDelegate messageHandler )
        {
            client.SetMessageHandler( async ( topic, pipeReader, payloadLength, qos, retain, cancellationToken ) =>
            {
                using( IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent( payloadLength ) )
                {
                    Memory<byte> buffer = memoryOwner.Memory[..payloadLength];
                    FillStatus res = await pipeReader.FillBuffer( buffer, cancellationToken );
                    if( res != FillStatus.Done ) throw new EndOfStreamException();
                    messageHandler( topic, buffer.Span, qos, retain );
                }
            } );
        }
    }
}
