using CK.MQTT.Client;
using CK.MQTT.P2P;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;

namespace CK.MQTT.Server
{
    class ClientWrapper : ServerMessageExchanger
    {
        readonly MqttServerClient _serverClient;

        public ClientWrapper( MqttServerClient serverClient, ProtocolConfiguration pConfig, Mqtt3ConfigurationBase config, IMqtt3Sink sink, IMqttChannel channel, IRemotePacketStore? remotePacketStore = null, ILocalPacketStore? localPacketStore = null )
            : base( pConfig, config, sink, channel, serverClient._outputTopicFilter, remotePacketStore, localPacketStore )
        {
            _serverClient = serverClient;
            Sink = new FilteringSinkWrapper( sink, _serverClient._inputTopicFilter );
        }

        protected override InputPump CreateInputPump( Reflex reflex )
            => new InterlacedInputPump(
                this,
                reflex,
                _serverClient._inputTopicFilter,
                _serverClient._subscriptionsCommand.Reader
            );

        protected override OutputProcessor CreateOutputProcessor()
            => new FilteringOutputProcessor( _serverClient._outputTopicFilter, this );
    }
}
