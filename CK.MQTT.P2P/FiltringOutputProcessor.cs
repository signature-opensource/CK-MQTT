using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.P2P
{
    class FiltringOutputProcessor : OutputProcessor
    {
        readonly ITopicFilter _filter;

        public FiltringOutputProcessor( ITopicFilter filter, ProtocolConfiguration pConfig, OutputPump outputPump, PipeWriter pipeWriter, ILocalPacketStore outgoingPacketStore ) : base( pConfig, outputPump, pipeWriter, outgoingPacketStore )
        {
            _filter = filter;
        }

        protected override async ValueTask<bool> SendAMessageFromQueueAsync( CancellationToken cancellationToken )
        {
            IOutgoingPacket? packet;
            FilterUpdatePacket? filterUpdatePacket;
            do
            {
                if( !OutputPump.ReflexesChannel.Reader.TryRead( out packet ) && !OutputPump.MessagesChannel.Reader.TryRead( out packet ) )
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

        public class FilterUpdatePacket : IOutgoingPacket
        {
            public FilterUpdatePacket( bool subscribe, string[] topics )
            {
                Subscribe = subscribe;
                Topics = topics;
            }

            public ushort PacketId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public QualityOfService Qos => throw new NotImplementedException();

            public bool IsRemoteOwnedPacketId => throw new NotImplementedException();

            public bool Subscribe { get; }
            public string[] Topics { get; }

            public uint GetSize( ProtocolLevel protocolLevel ) => throw new NotImplementedException();

            public ValueTask<WriteResult> WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken ) => throw new NotImplementedException();
        }
    }
}
