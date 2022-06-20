using CK.Core;
using CK.MQTT;
using CK.MQTT.Client;
using CK.MQTT.Packets;
using System.Diagnostics;

if( args.Length < 2 || args.Length > 3 )
{
    Console.WriteLine( $"Erreur, trouvé {args.Length}, il faut 2 à 3 arguments uniquement. Exemple: \"MqttTester.exe hostname port\"" );
    return;
}
if( !int.TryParse( args[1], out int port ) )
{
    Console.WriteLine( $"Le port '{port}' n'est pas un nombre." );
    return;
}

bool stressMax = args.Length > 2 && args[2] == "stress-max";

var buffer = new byte[500];
buffer = buffer.Select( ( s, i ) => (byte)i ).ToArray();
var client = new MqttClientAgent(
               ( sink ) => new LowLevelMqttClient( ProtocolConfiguration.Mqtt3, new Mqtt3ClientConfiguration()
               {
                   KeepAliveSeconds = 30,
                   DisconnectBehavior = DisconnectBehavior.AutoReconnect,
                   Credentials = new MqttClientCredentials( "test", true )
               }, sink, new TcpChannel( args[0], port ) )
);

var connectRes = await client.ConnectAsync();
if( connectRes.Status != ConnectStatus.Successful )
{
    Console.WriteLine( $"Error while connecting: {connectRes}" );
    return;
}
client.OnConnectionChange.Sync += OnConnectionChange;

void OnConnectionChange( IActivityMonitor monitor, DisconnectReason e )
{
    if( e == DisconnectReason.None ) return;
    Console.WriteLine( $"Error: Got disconnected:" + e );
    Environment.Exit( 1 );
}
Console.WriteLine( "Connected." );
int inFlight = 0;
for( int i = 0; i < 26000; i++ )
{
    if( stressMax )
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
        Console.WriteLine( "received ack for msg number: " + foo );
    } );
    while( inFlight > 100 && !stressMax )
    {
        await Task.Delay( 1000 );
        Console.WriteLine( "Waiting for response..." );
    }
}

await await client.PublishAsync( new SmallOutgoingApplicationMessage( "test-topic", QualityOfService.ExactlyOnce, false, buffer ) );

Console.WriteLine( "Done" );
