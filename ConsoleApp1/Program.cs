using CK.MQTT;
using CK.MQTT.Client;
using CK.MQTT.Packets;
using System.Diagnostics;
using System.Net;

var message = new SmallOutgoingApplicationMessage( "A", QualityOfService.AtMostOnce, false, new byte[] { 1 } );
var client = new MqttClientAgent(
    ( sink ) => new LowLevelMqttClient( ProtocolConfiguration.Mqtt3, new Mqtt3ClientConfiguration()
    {
        KeepAliveSeconds = 5000
    }, sink, new TcpChannel( new IPEndPoint(IPAddress.Loopback, 1883)) )
);
await client.ConnectAsync();
Console.WriteLine( "Starting" );
Stopwatch _stopwatch = Stopwatch.StartNew();
var lastElapsed = _stopwatch.Elapsed;
const int summaryCount = 100_000;
for( var i = 0; i < 100_000_000; i++ )
{

    await await client.PublishAsync( message );
    if( i % 100_000 == 0 )
    {
        var elapsed = _stopwatch.Elapsed - lastElapsed;
        Console.WriteLine( $"packet count {i:000000000} pps:{(summaryCount/elapsed.TotalSeconds):0000000.000}" );
        lastElapsed = _stopwatch.Elapsed;
    }
}

Console.WriteLine( "Finished qos2" );
await await client.PublishAsync( new SmallOutgoingApplicationMessage( "A", QualityOfService.AtLeastOnce, false, Array.Empty<byte>() ) );
Console.WriteLine( _stopwatch.ElapsedMilliseconds );
