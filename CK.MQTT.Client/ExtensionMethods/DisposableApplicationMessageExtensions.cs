using CK.Core;
using CK.MQTT.Client.Closures;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public static class DisposableApplicationMessageExtensions
    {
        public static void SetMessageHandler( this IMqtt3Client client, Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> handler )
            => client.SetMessageHandler( new DisposableMessageClosure( handler ).HandleMessageAsync );
    }
}
