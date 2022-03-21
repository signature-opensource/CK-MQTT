using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Net;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public static class SenderHelper
    {
        public static async ValueTask<Task<T?>> SendPacketAsync<T>( ILocalPacketStore store, OutputPump output, IOutgoingPacket packet )
        {
            return packet.Qos switch
            {
                QualityOfService.AtMostOnce => await PublishQoS0Async<T>( output, packet ),
                QualityOfService.AtLeastOnce => await StoreAndSendAsync<T>( output, store, packet, packet.Qos ),
                QualityOfService.ExactlyOnce => await StoreAndSendAsync<T>( output, store, packet, packet.Qos ),
                _ => throw new ArgumentException( "Invalid QoS." ),
            };
        }

        static async ValueTask<Task<T?>> PublishQoS0Async<T>( OutputPump output, IOutgoingPacket msg )
        {
            await output.QueueMessageAndWaitUntilSentAsync( msg );
            return Task.FromResult<T?>( default );
        }

        static async ValueTask<Task<T?>> StoreAndSendAsync<T>( OutputPump output, ILocalPacketStore messageStore, IOutgoingPacket msg, QualityOfService qos )
        {
            (Task<object?> ackReceived, IOutgoingPacket newPacket) = await messageStore.StoreMessageAsync( msg, qos );
            return SendAsync<T>( output, newPacket, ackReceived );
        }

        static async Task<T?> SendAsync<T>( OutputPump output, IOutgoingPacket packet, Task<object?> ackReceived )
        {
            await output.QueueMessageAndWaitUntilSentAsync( packet );
            object? res = await ackReceived;
            if( res is null ) return default;
            if( res is T a ) return a;
            //For example: it will throw if the client send a Publish, and the server answer a SubscribeAck with the same packet id as the publish.
            throw new ProtocolViolationException( "We received a packet id ack of an unexpected packet type." );
        }
    }
}
