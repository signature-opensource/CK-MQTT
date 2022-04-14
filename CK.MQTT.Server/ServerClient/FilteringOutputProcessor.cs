using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.P2P
{
    class FilteringOutputProcessor : OutputProcessor
    {
        readonly ITopicFilter _filter;

        public FilteringOutputProcessor( ITopicFilter filter, MessageExchanger messageExchanger ) : base( messageExchanger )
        {
            _filter = filter;
        }

        protected override async ValueTask<bool> SendAMessageFromQueueAsync( CancellationToken cancellationToken )
        {
            FilterUpdatePacket? filterUpdatePacket;
            do
            {
                if( !ReflexesChannel.Reader.TryRead( out IOutgoingPacket? packet ) && !MessagesChannel.Reader.TryRead( out packet ) )
                {
                    return false;
                }
                filterUpdatePacket = packet as FilterUpdatePacket;
                if( filterUpdatePacket != null )
                {
                    if( filterUpdatePacket.Subscribe )
                    {
                        foreach( var topic in filterUpdatePacket.Topics )
                        {
                            _filter.Subscribe( topic );
                        }
                    }
                    else
                    {
                        foreach( var topic in filterUpdatePacket.Topics )
                        {
                            _filter.Unsubscribe( topic );
                        }
                    }
                }
            }
            while( filterUpdatePacket != null );
            return await base.SendAMessageFromQueueAsync( cancellationToken );
        }
    }
}
