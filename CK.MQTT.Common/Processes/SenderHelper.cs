using CK.Core;
using CK.MQTT.Abstractions.Packets;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Stores;
using System;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Processes
{
    public static class SenderHelper
    {
        public static ValueTask<Task<T?>> SendPacket<T>(
            IActivityMonitor m,
            PacketStore messageStore,
            OutgoingMessageHandler output,
            IOutgoingPacketWithId packet,
            MqttConfiguration config )
            where T : class
            => packet.Qos switch
            {
                QualityOfService.AtMostOnce => PublishQoS0<T>( m, output, packet ),
                QualityOfService.AtLeastOnce => StoreAndSend<T>( m, output, messageStore, packet, config.WaitTimeoutMiliseconds ),
                QualityOfService.ExactlyOnce => StoreAndSend<T>( m, output, messageStore, packet, config.WaitTimeoutMiliseconds ),
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

        public static async ValueTask<Task<T?>> StoreAndSend<T>( IActivityMonitor m, OutgoingMessageHandler output,
            PacketStore messageStore, IOutgoingPacketWithId msg, int waitTimeoutMs )
            where T : class
        {
            (IOutgoingPacketWithId newPacket, Task<object?> ackReceived) = await messageStore.StoreMessageAsync( m, msg );
            return Send<T>( m, output, messageStore, newPacket, ackReceived, waitTimeoutMs );
        }

        public static async Task<T?> Send<T>( IActivityMonitor m,
            OutgoingMessageHandler output, PacketStore store, IOutgoingPacketWithId packet, Task<object?> ackReceived, int waitTimeoutMs )
            where T : class
        {
            do
            {
                await output.SendMessageAsync( packet );//We await that the message is actually sent on the wire.
                await Task.WhenAny( Task.Delay( waitTimeoutMs ), ackReceived );
            } while( !ackReceived.IsCompleted );
            return (T?)await ackReceived;//I don't really like this, but it's really handy.
        }
    }
}
