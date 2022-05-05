using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using CK.MQTT.Server;
using CK.MQTT.Server.OutgoingPackets;
using CK.MQTT.Stores;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server.ServerClient
{
    class FilteringOutputProcessor : OutputProcessor
    {
        readonly ITopicManager _topicManager;

        public FilteringOutputProcessor( ITopicManager topicManager, MessageExchanger messageExchanger ) : base( messageExchanger )
        {
            _topicManager = topicManager;
        }

        protected override async ValueTask<bool> SendAMessageFromQueueAsync( CancellationToken cancellationToken )
        {
            while( true )
            {
                if( !ReflexesChannel.Reader.TryPeek( out IOutgoingPacket? packet ) && !MessagesChannel.Reader.TryPeek( out packet ) )
                {
                    return false;
                }
                var subPacket = packet as InternalSubscribePacket;
                if( subPacket != null )
                {
                    await _topicManager.SubscribeAsync( subPacket.Topics );
                    await ReflexesChannel.Reader.ReadAsync( CancellationToken.None ); // there is a packet available so we consume it.
                    continue;
                }
                var unsubPacket = packet as InternalUnsubscribePacket;
                if( unsubPacket != null )
                {
                    await _topicManager.UnsubscribeAsync( unsubPacket.Topics );
                    await ReflexesChannel.Reader.ReadAsync( CancellationToken.None ); // there is a packet available so we consume it.
                    continue;
                }
                break;
            }
            return await base.SendAMessageFromQueueAsync( cancellationToken );
        }
    }
}
