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
        public static ValueTask<Task<T?>> SendPacketAsync<T>( IActivityMonitor? m, IOutgoingPacketStore store, OutputPump output, IOutgoingPacket packet )
            where T : class
        {
            IDisposableGroup? group = m?.OpenTrace( $"Sending a packet '{packet}'in QoS {packet.Qos}" );
            return packet.Qos switch
            {
                QualityOfService.AtMostOnce => PublishQoS0Async<T>( m, group, output, packet ),
                QualityOfService.AtLeastOnce => StoreAndSendAsync<T>( m, group, output, store, packet, packet.Qos ),
                QualityOfService.ExactlyOnce => StoreAndSendAsync<T>( m, group, output, store, packet, packet.Qos ),
                _ => throw new ArgumentException( "Invalid QoS." ),
            };
        }

        static async ValueTask<Task<T?>> PublishQoS0Async<T>( IActivityMonitor? m, IDisposableGroup? disposableGrp, OutputPump output, IOutgoingPacket msg ) where T : class
        {
            using( disposableGrp )
            using( m?.OpenTrace( "Executing Publish protocol with QoS 0." ) )
            {
                await output.QueueMessageAndWaitUntilSentAsync( m, msg );
                return Task.FromResult<T?>( null );
            }
        }

        static async ValueTask<Task<T?>> StoreAndSendAsync<T>( IActivityMonitor? m, IDisposableGroup? disposableGrp,
            OutputPump output, IOutgoingPacketStore messageStore, IOutgoingPacket msg, QualityOfService qos )
            where T : class
        {
            Task<object?> ackReceived;
            IOutgoingPacket newPacket;
            try
            {
                (ackReceived, newPacket) = await messageStore.StoreMessageAsync( m, msg, qos );
            }
            catch( Exception )
            {
                disposableGrp?.Dispose();
                throw;
            }
            return SendAsync<T>( m, disposableGrp, output, newPacket, ackReceived );
        }

        static async Task<T?> SendAsync<T>( IActivityMonitor? m, IDisposableGroup? disposableGrp, OutputPump output, IOutgoingPacket packet, Task<object?> ackReceived )
            where T : class
        {
            using( disposableGrp )
            {
                await output.QueueMessageAndWaitUntilSentAsync( m, packet );
                object? res = await ackReceived;
                if( res is null ) return null;
                if( res is T a ) return a;
            }
            //For example: it will throw if the client send a Publish, and the server answer a SubscribeAck with the same packet id as the publish.
            throw new ProtocolViolationException( "We received a packet id ack from an unexpected packet type." );
        }
    }
}
