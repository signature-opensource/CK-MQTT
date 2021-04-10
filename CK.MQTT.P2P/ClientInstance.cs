//using CK.Core;
//using CK.MQTT.P2P;
//using CK.MQTT.Pumps;
//using CK.MQTT.Stores;
//using System;
//using System.Collections.Generic;
//using System.IO.Pipelines;
//using System.Net;
//using System.Net.Sockets;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace CK.MQTT.Server
//{
//    class ClientInstance : Pumppeteer<ClientInstance.InstanceState>
//    {
//        public class InstanceState : StateHolder
//        {
//            public InstanceState( InputPump input, OutputPump output ) : base( input, output )
//            {
//            }
//        }

//        ClientInstance( MqttConfigurationBase config, IMqttChannel channel ) : base( config )
//        {
//            Channel = channel;
//        }

//        public static async ValueTask<ClientInstance> Connect( IActivityMonitor? m,
//            string clientAddress, IMqttChannel channel, ProtocolConfiguration pConfig, MqttServerConfiguration config )
//        {
//            // WIP.
//            ClientInstance instance = new( config, channel );
//            //ConnectReflex connReflex = new( config, clientAddress );
//            ReflexMiddlewareBuilder reflexBuilder = new();

//            //var input = new InputPump( instance, channel.DuplexPipe.Input, connReflex.HandleRequest );
//            //await connReflex.ConnectHandled; // TODO: connReflex should change the next reflex to the one handling AUTHENTICATE if required.
//            //string clientID = connReflex.ClientId;


//            //var output = new OutputPump( instance, pConfig, , channel.DuplexPipe.Output, connReflex.OutStore );

//            Reflex reflex = reflexBuilder.Build( instance.InvalidPacket );
//        }

//        // This is copy pasted from the client, could we avoid it ?
//        async ValueTask InvalidPacket( IInputLogger? m, InputPump sender, byte header, int packetSize, PipeReader reader, CancellationToken cancellationToken )
//        {
//            _ = await CloseAsync( DisconnectedReason.ProtocolError );
//            throw new ProtocolViolationException();
//        }

//        InputPump _inputPump;
//        OutputPump _outputPump;

//        public IMqttChannel Channel { get; }
//    }
//}
