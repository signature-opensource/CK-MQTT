using CK.MQTT;
using CK.MQTT.Client;
using CK.MQTT.Client.Middleware;
using Microsoft;

namespace ClientSample
{
    public class Program
    {
        public static async Task Main( string[] args )
        {
            for( int i = 0; i < 10000; i++ )
            {

                var messageWorker = new MessageWorker();
                var sink = new DefaultClientMessageSink( messageWorker.MessageWriter );
                var client = new LowLevelMQTTClient(
                        ProtocolConfiguration.MQTT3,
                        new MQTT3ClientConfiguration(),
                        sink,
                        new TcpChannel( "localhost", 1883 )
                    );
                await client.ConnectAsync( true );
            }
        }
    }
}
