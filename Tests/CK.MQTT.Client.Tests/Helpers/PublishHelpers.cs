using CK.Core;
using CK.MQTT.Packets;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{

    public class NewApplicationMessageClosure
    {
        readonly Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> _messageHandler;
        public NewApplicationMessageClosure( Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => _messageHandler = messageHandler;

        public async ValueTask HandleMessageAsync( string topic, PipeReader pipe, uint payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
        {
            Memory<byte> buffer = new byte[payloadLength];
            if( payloadLength != 0 )
            {
                ReadResult readResult = await pipe.ReadAtLeastAsync( (int)payloadLength, cancelToken );
                readResult.Buffer.Slice( 0, (int)payloadLength ).CopyTo( buffer.Span );
                pipe.AdvanceTo( readResult.Buffer.Slice( Math.Min( payloadLength, readResult.Buffer.Length ) ).Start );
            }
            await _messageHandler( null, new ApplicationMessage( topic, buffer, qos, retain ), cancelToken );
        }
    }
    static class ApplicationMessageExtensions
    {
        public static ValueTask<Task> PublishAsync( this TestMqttClient @this, ApplicationMessage message )
            => @this.PublishAsync( message.Topic, message.QoS, message.Retain, message.Payload );
    }
}
