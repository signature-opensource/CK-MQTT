using CK.Core;
using System;
using System.Net;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public static class SenderHelper
    {
        public static ValueTask<Task<T?>> SendPacket<T>( IActivityMonitor m, PacketStore messageStore, OutputPump output,
            IOutgoingPacketWithId packet, MqttConfiguration config )
            where T : class
            => packet.Qos switch
            {
                QualityOfService.AtMostOnce => PublishQoS0<T>( m, output, packet ),
                QualityOfService.AtLeastOnce => StoreAndSend<T>( m, output, messageStore, packet ),
                QualityOfService.ExactlyOnce => StoreAndSend<T>( m, output, messageStore, packet ),
                _ => throw new ArgumentException( "Invalid QoS." ),
            };

        public static async ValueTask<Task<T?>> PublishQoS0<T>( IActivityMonitor m, OutputPump output, IOutgoingPacketWithId msg )
            where T : class
        {
            using( m.OpenTrace()?.Send( "Executing Publish protocol with QoS 0." ) )
            {
                await output.SendMessageAsync( msg );
                return Task.FromResult<T?>( null );
            }
        }

        public static async ValueTask<Task<T?>> StoreAndSend<T>( IActivityMonitor m, OutputPump output, PacketStore messageStore, IOutgoingPacketWithId msg )
            where T : class
        {
            (IOutgoingPacketWithId newPacket, Task<object?> ackReceived) = await messageStore.StoreMessageAsync( m, msg );
            return Send<T>( output, newPacket, ackReceived );
        }

        public static async Task<T?> Send<T>( OutputPump output, IOutgoingPacketWithId packet, Task<object?> ackReceived )
            where T : class
        {
            await output.SendMessageAsync( packet );
            object? res = await ackReceived;
            if( res is null ) return null;
            if( res is T a ) return a;
            //For exemple: it will throw if the client send a Publish, and the server answer a SubscribeAck with the same packet id as the publish.
            throw new ProtocolViolationException( "We received a packet id ack from an unexpected packet type." );
        }
    }
}
