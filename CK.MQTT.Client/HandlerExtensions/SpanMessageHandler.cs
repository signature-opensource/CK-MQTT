using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static CK.Core.Extension.PipeReaderExtensions;

namespace CK.MQTT
{
    public static class SpanMessageHandler
    {
        public delegate void SpanMessageHandlerDelegate( string topic, ReadOnlySpan<byte> buffer, QualityOfService qos, bool retain );
        
        class HandlerClosure
        {
            readonly SpanMessageHandlerDelegate _messageHandler;

            public HandlerClosure( SpanMessageHandlerDelegate messageHandler )
            {
                _messageHandler = messageHandler;
            }
            public async ValueTask HandleMessage( string topic, PipeReader pipe, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
            {
                using( IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent( payloadLength ) )
                {
                    Memory<byte> buffer = memoryOwner.Memory[..payloadLength];
                    FillStatus res = await pipe.CopyToBuffer( buffer, cancelToken );
                    if( res != FillStatus.Done ) throw new EndOfStreamException();
                    _messageHandler( topic, buffer.Span, qos, retain );
                }
            }
        }
        public static void SetMessageHandler( this IMqtt3Client @this, SpanMessageHandlerDelegate messageHandler )
            => @this.SetMessageHandler( new HandlerClosure( messageHandler ).HandleMessage );

        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory @this, MqttConfiguration config, SpanMessageHandlerDelegate messageHandler )
            => @this.CreateMQTT3Client( config, new HandlerClosure( messageHandler ).HandleMessage );

        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory @this, MqttConfiguration config, SpanMessageHandlerDelegate messageHandler )
            => @this.CreateMQTT5Client( config, new HandlerClosure( messageHandler ).HandleMessage );

        public static IMqttClient CreateMQTTClient( this MqttClientFactory @this, MqttConfiguration config, SpanMessageHandlerDelegate messageHandler )
            => @this.CreateMQTTClient( config, new HandlerClosure( messageHandler ).HandleMessage );

    }
}
