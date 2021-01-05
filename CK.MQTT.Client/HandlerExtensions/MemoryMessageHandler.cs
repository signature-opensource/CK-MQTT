using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.Core.Extension;

namespace CK.MQTT
{
    public static class MemoryMessageHandler
    {
        class HandlerCancellabeClosure
        {
            readonly Func<IActivityMonitor, string, ReadOnlyMemory<byte>, QualityOfService, bool, CancellationToken, ValueTask> _messageHandler;
            public HandlerCancellabeClosure( Func<IActivityMonitor, string, ReadOnlyMemory<byte>, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            {
                _messageHandler = messageHandler;
            }

            public async ValueTask HandleMessage( IActivityMonitor m, string topic, PipeReader pipe, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
            {
                using( IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent( payloadLength ) )
                {
                    Memory<byte> buffer = memoryOwner.Memory[..payloadLength];
                    PipeReaderExtensions.FillStatus res = await pipe.CopyToBuffer( buffer, cancelToken );
                    if( res != PipeReaderExtensions.FillStatus.Done ) throw new EndOfStreamException();
                    await _messageHandler( m, topic, buffer, qos, retain, cancelToken );
                }
            }
        }

        public static void SetMessageHandler( this IMqtt3Client client, Func<IActivityMonitor, string, ReadOnlyMemory<byte>, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => client.SetMessageHandler( new HandlerCancellabeClosure( messageHandler ).HandleMessage );

        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory @this,
            MqttConfiguration config, Func<IActivityMonitor, string, ReadOnlyMemory<byte>, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => @this.CreateMQTT3Client( config, new HandlerCancellabeClosure( messageHandler ).HandleMessage );

        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory @this,
            MqttConfiguration config, Func<IActivityMonitor, string, ReadOnlyMemory<byte>, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => @this.CreateMQTT5Client( config, new HandlerCancellabeClosure( messageHandler ).HandleMessage );

        public static IMqttClient CreateMQTTClient( this MqttClientFactory @this,
            MqttConfiguration config, Func<IActivityMonitor, string, ReadOnlyMemory<byte>, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => @this.CreateMQTTClient( config, new HandlerCancellabeClosure( messageHandler ).HandleMessage );


        class HandlerClosure
        {
            readonly Func<IActivityMonitor, string, ReadOnlyMemory<byte>, QualityOfService, bool, ValueTask> _messageHandler;
            public HandlerClosure( Func<IActivityMonitor, string, ReadOnlyMemory<byte>, QualityOfService, bool, ValueTask> messageHandler )
                => _messageHandler = messageHandler;

            public async ValueTask HandleMessage( IActivityMonitor m, string topic, PipeReader pipe, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
            {
                using( IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent( payloadLength ) )
                {
                    Memory<byte> buffer = memoryOwner.Memory[..payloadLength];
                    PipeReaderExtensions.FillStatus res = await pipe.CopyToBuffer( buffer, cancelToken );
                    if( res != PipeReaderExtensions.FillStatus.Done ) throw new EndOfStreamException();
                    await _messageHandler( m, topic, buffer, qos, retain );
                }
            }
        }

        public static void SetMessageHandler( this IMqtt3Client client, Func<IActivityMonitor, string, ReadOnlyMemory<byte>, QualityOfService, bool, ValueTask> messageHandler )
            => client.SetMessageHandler( new HandlerClosure( messageHandler ).HandleMessage );

        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory factory, MqttConfiguration config, Func<IActivityMonitor, string, ReadOnlyMemory<byte>, QualityOfService, bool, ValueTask> messageHandler )
            => factory.CreateMQTT3Client( config, new HandlerClosure( messageHandler ).HandleMessage );

        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory factory, MqttConfiguration config, Func<IActivityMonitor, string, ReadOnlyMemory<byte>, QualityOfService, bool, ValueTask> messageHandler )
            => factory.CreateMQTT5Client( config, new HandlerClosure( messageHandler ).HandleMessage );

        public static IMqttClient CreateMQTTClient( this MqttClientFactory factory, MqttConfiguration config, Func<IActivityMonitor, string, ReadOnlyMemory<byte>, QualityOfService, bool, ValueTask> messageHandler )
            => factory.CreateMQTTClient( config, new HandlerClosure( messageHandler ).HandleMessage );

    }
}
