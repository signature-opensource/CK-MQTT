using System;
using System.Net;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public static class SenderHelper
    {
        public static ValueTask<Task<T?>> SendPacket<T>(
            IMqttLogger m,
            PacketStore messageStore,
            OutgoingMessageHandler output,
            IOutgoingPacketWithId packet,
            MqttConfiguration config )
            where T : class
            => packet.Qos switch
            {
                QualityOfService.AtMostOnce => PublishQoS0<T>( m, output, packet ),
                QualityOfService.AtLeastOnce => StoreAndSend<T>( m, output, messageStore, packet, config.WaitTimeoutMs ),
                QualityOfService.ExactlyOnce => StoreAndSend<T>( m, output, messageStore, packet, config.WaitTimeoutMs ),
                _ => throw new ArgumentException( "Invalid QoS." ),
            };

        public static async ValueTask<Task<T?>> PublishQoS0<T>( IMqttLogger m, OutgoingMessageHandler output, IOutgoingPacketWithId msg )
            where T : class
        {
            using( m.OpenTrace( "Executing Publish protocol with QoS 0." ) )
            {
                await output.SendMessageAsync( msg );
                return Task.FromResult<T?>( null );
            }
        }

        public static async ValueTask<Task<T?>> StoreAndSend<T>( IMqttLogger m, OutgoingMessageHandler output, PacketStore messageStore,
            IOutgoingPacketWithId msg, int waitTimeoutMs )
            where T : class
        {
            (IOutgoingPacketWithId newPacket, Task<object?> ackReceived) = await messageStore.StoreMessageAsync( m, msg );
            return Send<T>( m, output, messageStore, newPacket, ackReceived, waitTimeoutMs );
        }

        public static async Task<T?> Send<T>( IMqttLogger m,
            OutgoingMessageHandler output, PacketStore store, IOutgoingPacketWithId packet, Task<object?> ackReceived, int waitTimeoutMs )
            where T : class
        {
            await output.SendMessageAsync( packet );
            await Task.WhenAny( Task.Delay( waitTimeoutMs ), ackReceived );
            while( !ackReceived.IsCompleted )
            {
                packet = await store.GetMessageByIdAsync( m, packet.PacketId );
                await Task.WhenAny( Task.Delay( waitTimeoutMs ), ackReceived );
            }
            object? res = await ackReceived;
            if( res is null ) return null;
            if( res is T a ) return a;
            throw new ProtocolViolationException( "Received ack is not of the expected type." );
        }
    }
}
