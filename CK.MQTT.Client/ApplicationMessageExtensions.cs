using CK.Core;
using CK.MQTT.Client;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static CK.Core.Extension.PipeReaderExtensions;

namespace CK.MQTT.Client
{

    public static class ApplicationMessageExtensions
    {
        public static ValueTask<Task> PublishAsync( this IMqtt3Client @this, IActivityMonitor m, ApplicationMessage message )
            => @this.PublishAsync( m, message.Topic, message.QoS, message.Retain, message.Payload );

        class HandlerCancellableClosure
        {
            readonly Func<IActivityMonitor, ApplicationMessage, CancellationToken, ValueTask> _messageHandler;
            public HandlerCancellableClosure( Func<IActivityMonitor, ApplicationMessage, CancellationToken, ValueTask> messageHandler )
                => _messageHandler = messageHandler;

            public async ValueTask HandleMessage( IActivityMonitor m,
                string topic, PipeReader pipe, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
            {
                Memory<byte> memory = new( new byte[payloadLength] );
                FillStatus status = await pipe.CopyToBufferAsync( memory, cancelToken );
                if( status != FillStatus.Done ) throw new InvalidOperationException( "Unexpected partial read." );
                await _messageHandler( m, new ApplicationMessage( topic, memory, qos, retain ), cancelToken );
            }
        }

        public static void SetMessageHandler( this IMqtt3Client @this, Func<IActivityMonitor, ApplicationMessage, CancellationToken, ValueTask> handler )
            => @this.SetMessageHandler( new HandlerCancellableClosure( handler ).HandleMessage );

        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory factory, MqttClientConfiguration config, Func<IActivityMonitor, ApplicationMessage, CancellationToken, ValueTask> handler )
            => factory.CreateMQTT3Client( config, new HandlerCancellableClosure( handler ).HandleMessage );

        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory @this, MqttClientConfiguration config, Func<IActivityMonitor, ApplicationMessage, CancellationToken, ValueTask> handler )
            => @this.CreateMQTT5Client( config, new HandlerCancellableClosure( handler ).HandleMessage );

        public static IMqttClient CreateMQTTClient( this MqttClientFactory @this, MqttClientConfiguration config, Func<IActivityMonitor, ApplicationMessage, CancellationToken, ValueTask> handler )
            => @this.CreateMQTTClient( config, new HandlerCancellableClosure( handler ).HandleMessage );

        class HandlerClosure
        {
            readonly Func<IActivityMonitor, ApplicationMessage, ValueTask> _messageHandler;
            public HandlerClosure( Func<IActivityMonitor, ApplicationMessage, ValueTask> messageHandler )
                => _messageHandler = messageHandler;

            public async ValueTask HandleMessage( IActivityMonitor m,
                string topic, PipeReader pipe, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
            {
                Memory<byte> memory = new( new byte[payloadLength] );
                FillStatus status = await pipe.CopyToBufferAsync( memory, cancelToken );
                if( status != FillStatus.Done ) throw new InvalidOperationException( "Unexpected partial read." );
                await _messageHandler( m, new ApplicationMessage( topic, memory, qos, retain ) );
            }
        }

        public static void SetMessageHandler( this IMqtt3Client @this, Func<IActivityMonitor, ApplicationMessage, ValueTask> handler )
            => @this.SetMessageHandler( new HandlerClosure( handler ).HandleMessage );

        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory @this, MqttClientConfiguration config, Func<IActivityMonitor, ApplicationMessage, ValueTask> handler )
            => @this.CreateMQTT3Client( config, new HandlerClosure( handler ).HandleMessage );

        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory @this, MqttClientConfiguration config, Func<IActivityMonitor, ApplicationMessage, ValueTask> handler )
            => @this.CreateMQTT5Client( config, new HandlerClosure( handler ).HandleMessage );

        public static IMqttClient CreateMQTTClient( this MqttClientFactory @this, MqttClientConfiguration config, Func<IActivityMonitor, ApplicationMessage, ValueTask> handler )
            => @this.CreateMQTTClient( config, new HandlerClosure( handler ).HandleMessage );
    }
}
