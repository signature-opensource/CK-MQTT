using CK.Core;
using System.Threading.Tasks;
using static CK.MQTT.Client.MQTTMessageSink;

namespace CK.MQTT.Client.Middleware
{
    class MessageExchangerLoggingMiddleware : IAgentMessageMiddleware
    {
        public ValueTask<bool> HandleAsync( IActivityMonitor m, object? message )
        {
            switch( message )
            {
                case UnparsedExtraData extraData:
                    m.Warn( $"There was {extraData.UnparsedData.Length} bytes unparsed in Packet {extraData.PacketId}." );
                    return new ValueTask<bool>( true );
                case QueueFullPacketDestroyed queueFullPacketDestroyed:
                    m.Warn( $"Because the queue is full, {queueFullPacketDestroyed.PacketType} with id {queueFullPacketDestroyed.PacketId} has been dropped. " );
                    return new ValueTask<bool>( true );
                default:
                    return new ValueTask<bool>( false );
            }
        }
        public ValueTask DisposeAsync() => new ValueTask();
    }
}
