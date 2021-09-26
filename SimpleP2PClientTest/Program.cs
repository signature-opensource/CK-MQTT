using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using CK.MQTT;
using CK.MQTT.Client;
using CK.MQTT.P2P;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleP2PClientTest
{
    class Program
    {
        async static ValueTask MessageHandlerDelegate( IActivityMonitor? m, ApplicationMessage msg, CancellationToken cancellationToken )
        {
            System.Console.WriteLine( msg.Topic + Encoding.UTF8.GetString( msg.Payload.Span ) );
        }
        static async Task Main()
        {
            //test.mosquitto.org
            var cfg = new GrandOutputConfiguration();
            cfg.Handlers.Add( new ConsoleConfiguration() );
            GrandOutput.EnsureActiveDefault( cfg );

            ActivityMonitor m = new();
            P2PClient client = P2PMqttClient.Factory.CreateMQTT3Client( new MqttClientConfiguration( "localhost:1883" )
            {
                KeepAliveSeconds = 0
            }, MessageHandlerDelegate );
            await client.AcceptClientAsync( m, new TcpChannelListener( new TcpListener( IPAddress.Any, 1883 ) ), default );
            //while( true )
            //{
            //    string line = System.Console.ReadLine();
            //    await client.PublishAsync( m, new ApplicationMessage( "foo", Encoding.UTF8.GetBytes( line ), QualityOfService.ExactlyOnce, false ) );
            //}
            await Task.Delay( -1 );
            await client.DisconnectAsync( m, true, true, default );
        }
    }
}
