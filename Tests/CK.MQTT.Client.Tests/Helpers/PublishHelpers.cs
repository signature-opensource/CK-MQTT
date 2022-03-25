using CK.Core;
using CK.MQTT.Packets;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    public class BaseHandlerClosure
    {
        readonly Func<ApplicationMessage, CancellationToken, ValueTask> _messageHandler;
        public BaseHandlerClosure( Func<ApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => _messageHandler = messageHandler;

        public async ValueTask HandleMessageAsync( string topic, PipeReader pipe, uint payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
        {
            Memory<byte> memory = new( new byte[payloadLength] );
            if( !memory.IsEmpty )
            {
                ReadResult readResult = await pipe.ReadAtLeastAsync( (int)payloadLength, cancelToken );
                if( readResult.IsCanceled || readResult.IsCompleted && readResult.Buffer.Length < memory.Length )
                {
                    pipe.AdvanceTo( readResult.Buffer.Slice( Math.Min( memory.Length, readResult.Buffer.Length ) ).End );
                    return;
                }
                ReadOnlySequence<byte> sliced = readResult.Buffer.Slice( 0, memory.Length );
                sliced.CopyTo( memory.Span );
                pipe.AdvanceTo( sliced.End );

            }
            await _messageHandler( new ApplicationMessage( topic, memory, qos, retain ), cancelToken );
        }
    }

    public class DisposableMessageClosure
    {
        readonly Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> _messageHandler;
        public DisposableMessageClosure( Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => _messageHandler = messageHandler;

        public async ValueTask HandleMessageAsync( string topic, PipeReader pipe, uint payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
        {
            IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent( (int)payloadLength );
            Memory<byte> memory = memoryOwner.Memory[..(int)payloadLength];
            if( !memory.IsEmpty )
            {
                ReadResult readResult = await pipe.ReadAtLeastAsync( (int)payloadLength, cancelToken );
                if( readResult.IsCanceled || readResult.IsCompleted && readResult.Buffer.Length < memory.Length )
                {
                    pipe.AdvanceTo( readResult.Buffer.Slice( Math.Min( memory.Length, readResult.Buffer.Length ) ).End );
                    return;
                }
                ReadOnlySequence<byte> sliced = readResult.Buffer.Slice( 0, memory.Length );
                sliced.CopyTo( memory.Span );
                pipe.AdvanceTo( sliced.End );

            }
            await _messageHandler( null, new DisposableApplicationMessage( topic, memory, qos, retain, memoryOwner ), cancelToken );
        }
    }

    static class SimpleOutgoingApplicationMessageExtensions
    {
        public static async ValueTask<Task> PublishAsync( this TestMqttClient client, string topic, QualityOfService qos, bool retain,
            Func<uint> getPayloadSize, PayloadWriterDelegate payloadWriter ) //Async required to convert wrapped Task<object> to Task.
        {
            return await client.PublishAsync( new BasicOutgoingApplicationMessage( topic, qos, retain, getPayloadSize, payloadWriter ) );
        }

        public static async ValueTask<Task> PublishAsync( this IMqtt5Client client, string topic, QualityOfService qos, bool retain, //publish values
            Func<uint> getPayloadSize, PayloadWriterDelegate payloadWriter, //payload
            string? responseTopic = null, ushort correlationDataSize = 0, SpanAction? correlationDataWriter = null ) //properties
        {
            MqttBinaryWriter.ThrowIfInvalidMQTTString( topic );
            return await client.PublishAsync(
                new BasicOutgoingApplicationMessage( topic, qos, retain, getPayloadSize, payloadWriter, responseTopic, correlationDataSize, correlationDataWriter ) );
        }
    }

    static class SmallOutgoingApplicationMessageExtensions
    {
        public static async ValueTask<Task> PublishAsync( this TestMqttClient client, string topic, QualityOfService qos, bool retain, ReadOnlyMemory<byte> payload )
        {
            return await client.PublishAsync( new SmallOutgoingApplicationMessage( topic, qos, retain, payload ) );
        }

        public static async ValueTask<Task> PublishAsync( this IMqtt5Client client, string topic, QualityOfService qos, bool retain, ReadOnlyMemory<byte> payload,
            string? responseTopic = null, ushort correlationDataSize = 0, SpanAction? correlationDataWriter = null ) //properties
        {
            MqttBinaryWriter.ThrowIfInvalidMQTTString( topic );
            return await client.PublishAsync( new SmallOutgoingApplicationMessage( topic, qos, retain, payload, responseTopic, correlationDataSize, correlationDataWriter ) );
        }
    }
    public class NewApplicationMessageClosure
    {
        readonly Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> _messageHandler;
        public NewApplicationMessageClosure( Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => _messageHandler = messageHandler;

        public async ValueTask HandleMessageAsync( string topic, PipeReader pipe, uint payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
        {
            Memory<byte> buffer = new byte[payloadLength];
            ReadResult readResult = await pipe.ReadAtLeastAsync( (int)payloadLength, cancelToken );
            readResult.Buffer.CopyTo( buffer.Span );
            pipe.AdvanceTo( readResult.Buffer.Slice( Math.Min( payloadLength, readResult.Buffer.Length ) ).End );
            await _messageHandler( null, new ApplicationMessage( topic, buffer, qos, retain ), cancelToken );
        }
    }
    static class ApplicationMessageExtensions
    {
        public static ValueTask<Task> PublishAsync( this TestMqttClient @this, ApplicationMessage message )
            => @this.PublishAsync( message.Topic, message.QoS, message.Retain, message.Payload );
    }
}
