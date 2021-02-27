using CK.Core;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    class ClientInstance : Pumppeteer<ClientInstance.InstanceState>
    {
        public class InstanceState : StateHolder
        {
            public InstanceState( InputPump input, OutputPump output ) : base( input, output )
            {
            }
        }

        ClientInstance( MqttConfigurationBase config, IMqttChannel channel ) : base( config )
        {
            Channel = channel;
        }

        public static ValueTask<ClientInstance> Connect( IActivityMonitor? m,
            string clientAddress,
            IMqttChannel channel, ProtocolConfiguration pConfig, MqttServerConfiguration configurationBase )
        {
            ClientInstance instance = new( configurationBase, channel );
            ConnectReflex connReflex = new();
            var input = new InputPump( instance, channel.DuplexPipe.Input, connReflex.HandleRequest );
            var output = new OutputPump( instance, pConfig, todo, channel.DuplexPipe.Output, todo );

            configurationBase.StoreFactory.GetStores(clientAddress, )
        }

        InputPump _inputPump;
        OutputPump _outputPump;

        public IMqttChannel Channel { get; }
    }
}
