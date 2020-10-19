using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CK.Core.Extension;

namespace CK.MQTT
{
    public static class MemoryMessageHandler
    {
        public delegate ValueTask MemoryMessageHandlerDelegate( string topic, ReadOnlyMemory<byte> buffer, QualityOfService qos, bool retain, CancellationToken cancellationToken = default );

        class HandlerClosure
        {
            readonly MemoryMessageHandlerDelegate _messageHandler;
            public HandlerClosure( MemoryMessageHandlerDelegate messageHandler )
            {
                _messageHandler = messageHandler;
            }

            public async ValueTask HandleMessage( string topic, PipeReader pipe, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
            {
                using( IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent( payloadLength ) )
                {
                    Memory<byte> buffer = memoryOwner.Memory[..payloadLength];
                    PipeReaderExtensions.FillStatus res = await pipe.FillBuffer( buffer, cancelToken );
                    if( res != PipeReaderExtensions.FillStatus.Done ) throw new EndOfStreamException();
                    await _messageHandler( topic, buffer, qos, retain, cancelToken );
                }
            }
        }

        public static void SetMessageHandler( this IMqtt3Client client, MemoryMessageHandlerDelegate messageHandler )
            => client.SetMessageHandler( new HandlerClosure( messageHandler ).HandleMessage );

        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory factory, MqttConfiguration config, MemoryMessageHandlerDelegate messageHandler )
            => factory.CreateMQTT3Client( config, new HandlerClosure( messageHandler ).HandleMessage );

        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory factory, MqttConfiguration config, MemoryMessageHandlerDelegate messageHandler )
            => factory.CreateMQTT5Client( config, new HandlerClosure( messageHandler ).HandleMessage );

        public static IMqttClient CreateMQTTClient( this MqttClientFactory factory, MqttConfiguration config, MemoryMessageHandlerDelegate messageHandler )
            => factory.CreateMQTTClient( config, new HandlerClosure( messageHandler ).HandleMessage );
    }
}
