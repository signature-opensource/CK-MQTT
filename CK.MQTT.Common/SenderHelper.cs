using CK.Core;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Net;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public static class SenderHelper
    {
        public static async ValueTask<Task<T?>> SendPacketAsync<T>( IActivityMonitor? m, ILocalPacketStore store, OutputPump output, IOutgoingPacket packet )
            where T : class
        {
            using( m?.OpenTrace( $"Sending a packet '{packet}' in QoS {packet.Qos}" ) )
            {

                return packet.Qos switch
                {
                    QualityOfService.AtMostOnce => await PublishQoS0Async<T>( m, output, packet ),
                    QualityOfService.AtLeastOnce => await StoreAndSendAsync<T>( m, output, store, packet, packet.Qos ),
                    QualityOfService.ExactlyOnce => await StoreAndSendAsync<T>( m, output, store, packet, packet.Qos ),
                    _ => throw new ArgumentException( "Invalid QoS." ),
                };
            }
        }

        static async ValueTask<Task<T?>> PublishQoS0Async<T>( IActivityMonitor? m, OutputPump output, IOutgoingPacket msg ) where T : class
        {
            using( m?.OpenTrace( "Executing Publish protocol with QoS 0." ) )
            {
                await output.QueueMessageAndWaitUntilSentAsync( m, msg );
                return Task.FromResult<T?>( null );
            }
        }

        static async ValueTask<Task<T?>> StoreAndSendAsync<T>( IActivityMonitor? m,
            OutputPump output, ILocalPacketStore messageStore, IOutgoingPacket msg, QualityOfService qos )
            where T : class
        {
            (Task<object?> ackReceived, IOutgoingPacket newPacket) = await messageStore.StoreMessageAsync( m, msg, qos );
            return SendAsync<T>( output, newPacket, ackReceived );
        }

        static async Task<T?> SendAsync<T>( OutputPump output, IOutgoingPacket packet, Task<object?> ackReceived )
            where T : class
        {
            // TODO: Investigate if we can log in a way or another there.
            await output.QueueMessageAndWaitUntilSentAsync( null, packet );
            object? res = await ackReceived;
            if( res is null ) return null;
            if( res is T a ) return a;
            //For example: it will throw if the client send a Publish, and the server answer a SubscribeAck with the same packet id as the publish.
            throw new ProtocolViolationException( "We received a packet id ack of an unexpected packet type." );
        }
    }
}
