using CK.MQTT;
using CK.MQTT.Client;

namespace ClientSample
{
    public class Program
    {
        public static async Task Main( string[] args )
        {
            for( int i = 0; i < 10000; i++ )
            {

                var client = new MQTTClientAgent(
                    ( sink ) => new LowLevelMQTTClient( ProtocolConfiguration.MQTT3,
                                                       new MQTT3ClientConfiguration()
                                                       {
                                                           Credentials = new MQTTClientCredentials( "test-client", true )
                                                       },
                                                       sink,
                                                       new TcpChannel( "localhost", 1883 ) ) );
                await client.ConnectAsync();
            }
        }
    }
}
