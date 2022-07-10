using CK.Core;
using CK.MQTT;
using CK.MQTT.Client;
using CK.MQTT.Packets;

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

client.OnConnectionChange.Sync += OnConnectionChange;
var backgroundRedirect = BackgroundRedirect();
var connectRes = await client.ConnectAsync();

async Task BackgroundRedirect()
{
    Random rnd = new Random( 42 );
    TcpClient source = await accept;
    TcpClient target = new TcpClient( "localhost", 1883 );
    using( var destStream = target.GetStream() )
    using( var sourceStream = source.GetStream() )
    using( var fsStream = File.OpenWrite( "dump.bin" ) )
    {
        var incoming = destStream.CopyToAsync( sourceStream );
        var buffer = new byte[10];
        while( true )
        {
            var size = await sourceStream.ReadAsync( buffer );
            var tmpBuffer = buffer[0..size];
            await destStream.WriteAsync( tmpBuffer );
            await fsStream.WriteAsync( tmpBuffer );
            await fsStream.FlushAsync();
            if( tmpBuffer.Length == 0 ) break;
            await Task.Delay( 5 );
            var nxt = rnd.Next( 100 );
            if( nxt == 20 )
            {
                Console.WriteLine( "Big wait" );
                await Task.Delay( 5000 );
            }
        }
        await incoming;
    }
}
if( connectRes.Status != ConnectStatus.Successful )
{
    Console.WriteLine( $"Error while connecting: {connectRes}" );
    return;
}
<<<<<<< HEAD
Console.WriteLine( "Connected" );
=======
>>>>>>> origin/develop

void OnConnectionChange( IActivityMonitor monitor, DisconnectReason e )
{
    Console.WriteLine( $"Connect changed:" + e );
}
<<<<<<< HEAD
await Task.Delay( 100000 );
=======
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

Console.WriteLine( "Done" );
await backgroundRedirect;
Console.WriteLine( "Background redirect done." );
>>>>>>> origin/develop
