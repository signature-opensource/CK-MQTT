using CK.Core;
using CK.MQTT;
using CK.MQTT.Client;
using CK.MQTT.Packets;
using System.Net.Sockets;

int port = 1883;
var buffer = new byte[500];
buffer = buffer.Select( ( s, i ) => (byte)i ).ToArray();
var client = new MqttClientAgent(
               ( sink ) => new LowLevelMqttClient( ProtocolConfiguration.Mqtt3, new Mqtt3ClientConfiguration()
               {
                   KeepAliveSeconds = 3000,
                   DisconnectBehavior = DisconnectBehavior.AutoReconnect,
                   Credentials = new MqttClientCredentials( "test", true ),
                   WaitTimeoutMilliseconds = 500000
               }, sink, new TcpChannel( "localhost", port ) )
);
client.OnConnectionChange.Sync += OnConnectionChange;

var connectRes = await client.ConnectAsync();

void OnConnectionChange( IActivityMonitor monitor, DisconnectReason e )
{
    Console.WriteLine( $"Connect changed:" + e );
}
Console.WriteLine( "Connected." );
int inFlight = 0;
for( int i = 0; i < 26000; i++ )
{
    if( false )
    {
        if( i % 1000 == 0 )
        {
            Console.WriteLine( "Sent 1000 messages." );
        }
    }
    else
    {
        if( i % 100 == 0 )
        {
            Console.WriteLine( "Sent 100 messages." );
        }
    }

    var task = await client.PublishAsync( new SmallOutgoingApplicationMessage( "test-topic", QualityOfService.AtLeastOnce, false, buffer ) );
    Interlocked.Increment( ref inFlight );
    var foo = i;
    _ = task.ContinueWith( ( a ) =>
    {
        Interlocked.Decrement( ref inFlight );
        //Console.WriteLine( "received ack for msg number: " + foo );
    } );
    while( inFlight > 100 && !false )
    {
        await Task.Delay( 1000 );
        Console.WriteLine( "Waiting for response..." );
    }
}

await await client.PublishAsync( new SmallOutgoingApplicationMessage( "test-topic", QualityOfService.ExactlyOnce, false, buffer ) );
