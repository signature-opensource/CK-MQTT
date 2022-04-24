using CK.MQTT;
using CK.MQTT.Client;
using CK.MQTT.Packets;
using System.Diagnostics;

var message = new SmallOutgoingApplicationMessage( "A", QualityOfService.AtMostOnce, false, Array.Empty<byte>() );
var client = new MqttClientAgent(
               ( sink ) => new LowLevelMqttClient( ProtocolConfiguration.Mqtt3, new Mqtt3ClientConfiguration()
               {
                   KeepAliveSeconds = 0
               }, sink, new TcpChannel( "localhost", 1883 ) )
    );
await client.ConnectAsync();
Console.WriteLine( "Starting" );
Stopwatch _stopwatch = Stopwatch.StartNew();
for( var i = 0; i < 100_000; i++ )
{
    await await client.PublishAsync( message );
}

Console.WriteLine( "Finished qos2" );
await await client.PublishAsync( new SmallOutgoingApplicationMessage( "A", QualityOfService.AtLeastOnce, false, Array.Empty<byte>() ) );
Console.WriteLine( _stopwatch.ElapsedMilliseconds );
