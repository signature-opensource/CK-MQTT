using CK.MQTT;
using CK.MQTT.Client;
using CK.MQTT.Packets;
using System.Diagnostics;

var buffer = new byte[500];
buffer = buffer.Select( ( s, i ) => (byte)i ).ToArray();
var message = new SmallOutgoingApplicationMessage( "A", QualityOfService.AtMostOnce, false, buffer );
var client = new MqttClientAgent(
               ( sink ) => new LowLevelMqttClient( ProtocolConfiguration.Mqtt3, new Mqtt3ClientConfiguration()
               {
                   KeepAliveSeconds = 0
               }, sink, new TcpChannel( "localhost", 1883 ) )
);
var sendCount = 50_000;
int msgCount = 0;
var tcs = new TaskCompletionSource();
Stopwatch readStopWatch = new();
client.OnMessage.Simple.Sync += OnMessage_Sync;
void OnMessage_Sync( CK.Core.IActivityMonitor monitor, ApplicationMessage e )
{
    if( msgCount++ == 0 ) readStopWatch.Start();
    if( msgCount == sendCount )
    {
        tcs.TrySetResult();
    }
}

var connectRes = await client.ConnectAsync();
Console.WriteLine( connectRes );
var res = await await client.SubscribeAsync( new Subscription( "A", QualityOfService.AtMostOnce ) );
if( res != SubscribeReturnCode.MaximumQoS0 ) throw new InvalidOperationException();
Console.WriteLine( "Starting" );
Stopwatch _stopwatch = Stopwatch.StartNew();
int i;
for( i = 0; i < sendCount; i++ )
{
    await await client.PublishAsync( message );
}
Console.WriteLine( $"Finished sending. sent in:{_stopwatch.Elapsed} receivedWhenSending:{msgCount}" );
await tcs.Task;
Console.WriteLine( $"Finish reading, read in: {_stopwatch.Elapsed}" );
GC.Collect( 2, GCCollectionMode.Forced );
Console.WriteLine( $"Allocated: {GC.GetTotalAllocatedBytes( true )}" );
Console.WriteLine( $"0:{GC.CollectionCount( 0 )}" );
Console.WriteLine( $"1:{GC.CollectionCount( 1 )}" );
Console.WriteLine( $"2:{GC.CollectionCount( 2 )}" );
