using CK.Core;
using CK.MQTT.Abstractions.Packets;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Stores;
using System;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Processes
{
    public static class SendQoSPacketProcess
    {
        /// <summary>
        /// Execute the publish protocol with the given QoS.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="channel">The channel used to send and receive messages.</param>
        /// <param name="messageStore">The message store to use to store messages.</param>
        /// <param name="topic">The topic of the message to send.</param>
        /// <param name="payload">The payload of the message to send.</param>
        /// <param name="qos">The QoS of the message to send.</param>
        /// <param name="retain">The retain flag that we will send in the packet.</param>
        /// <returns>A <see cref="Task{Task}"/> that complete when the guarantee of the QoS is fulfilled.
        /// The result of this <see cref="Task"/> that complete when the publish process is completed. </returns>
        public static ValueTask<Task<T?>> SendPacket<T>(
            IActivityMonitor m,
            PacketStore messageStore,
            OutgoingMessageHandler output,
            IOutgoingPacketWithId packet,
            int waitTimeoutMs )
            where T : class
            => packet.Qos switch
            {
                QualityOfService.AtMostOnce => PublishQoS0<T>( m, output, packet ),
                QualityOfService.AtLeastOnce => StoreAndPublishQoS1Or2<T>( m, output, messageStore, packet, waitTimeoutMs ),
                QualityOfService.ExactlyOnce => StoreAndPublishQoS1Or2<T>( m, output, messageStore, packet, waitTimeoutMs ),
                _ => throw new ArgumentException( "Invalid QoS." ),
            };

        public static async ValueTask<Task<T?>> PublishQoS0<T>( IActivityMonitor m, OutgoingMessageHandler output, IOutgoingPacketWithId msg )
            where T : class
        {
            using( m.OpenTrace( "Executing Publish protocol with QoS 0." ) )
            {
                await output.SendMessageAsync( msg );
                return Task.FromResult<T?>( null );
            }
        }

        public static async ValueTask<Task<T?>> StoreAndPublishQoS1Or2<T>( IActivityMonitor m, OutgoingMessageHandler output,
            PacketStore messageStore, IOutgoingPacketWithId msg, int waitTimeoutMs )
            where T : class
        {
            using( m.OpenTrace( "Sending a packet with QoS 1 or 2." ) )
            {
                IOutgoingPacketWithId newPacket = await messageStore.StoreMessageAsync( m, msg );//store the message
                //Now we can guarantee the At Least Once, the message have been stored.
                //We return the Task representing the rest of the protocol.
                return PublishQoS1Or2<T>( m, output, messageStore, newPacket, waitTimeoutMs );
            }
        }

        public static async Task<T?> PublishQoS1Or2<T>( IActivityMonitor m,
            OutgoingMessageHandler output, PacketStore store, IOutgoingPacketWithId packet, int waitTimeoutMs )
            where T : class
        {
            Task<object?> task = store.GetAwaiterById( packet.PacketId )!;
            do
            {
                await output.SendMessageAsync( packet );//We await that the message is actually sent on the wire.
                await Task.WhenAny( Task.Delay( waitTimeoutMs ), task );
            } while( !task.IsCompleted );
            return (T?)await task;
        }
    }
}
