using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.Client.Closures;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public static class ApplicationMessageExtensions
    {
        public static ValueTask<Task> PublishAsync( this IMqtt3Client @this, IActivityMonitor? m, ApplicationMessage message )
            => @this.PublishAsync( m, message.Topic, message.QoS, message.Retain, message.Payload );

        public static void SetMessageHandler( this IMqtt3Client @this, Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> handler )
            => @this.SetMessageHandler( new BaseHandlerClosure( handler ).HandleMessageAsync );
    }
}
