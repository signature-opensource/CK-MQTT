using CK.Core;
using CK.MQTT;
using CK.MQTT.Client;
using CK.MQTT.Packets;

var buffer = new byte[500];
buffer = buffer.Select( ( s, i ) => (byte)i ).ToArray();
var client = new MqttClientAgent(
               ( sink ) => new LowLevelMqttClient( ProtocolConfiguration.Mqtt3, new Mqtt3ClientConfiguration()
               {
                   KeepAliveSeconds = 1,
                   WaitTimeoutMilliseconds = 500,
                   DisconnectBehavior = DisconnectBehavior.AutoReconnect,
                   Credentials = new MqttClientCredentials( "test", true )
               }, sink, new TcpChannel( "192.168.1.46", 1883 ) )
);
client.OnConnectionChange.Sync += OnConnectionChange;

var connectRes = await client.ConnectAsync();
if( connectRes.Status != ConnectStatus.Successful )
{
    Console.WriteLine( $"Error while connecting: {connectRes}" );
    return;
}
Console.WriteLine( "Connected" );

void OnConnectionChange( IActivityMonitor monitor, DisconnectReason e )
{
    Console.WriteLine( $"Connect changed:" + e );
}
await Task.Delay( 100000 );
