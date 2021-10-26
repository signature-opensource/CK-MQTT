using CK.Core;
using CK.MQTT.Client.Closures;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public static class DisposableApplicationMessageExtensions
    {

        /// <param name="client">The client that will send the message.</param>
        /// <param name="m">The monitor used to log.</param>
        /// <param name="message">The message that will be sent. It will be disposed after being stored.</param>
        /// <returns>A ValueTask, that complete when the message has been stored, containing a Task that complete when the publish has been ack.
        /// On QoS 0, the message is directly sent and the returned Task is Task.CompletedTask.
        /// </returns>
        public static async ValueTask<Task> PublishAsync( this IMqtt3Client client, IActivityMonitor? m, DisposableApplicationMessage message )
        {
            Task task = await client.PublishAsync( m, message.Topic, message.QoS, message.Retain, message.Payload ); //The packet has been stored
            message.Dispose(); // So we can dispose after it has been stored.
            return task; // The task we return complete when the packet has been acked.
        }

        public static void SetMessageHandler( this IMqtt3Client client, Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> handler )
            => client.SetMessageHandler( new DisposableMessageClosure( handler ).HandleMessageAsync );
    }
}
