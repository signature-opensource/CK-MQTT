using CK.Core;
using CK.MQTT.Client.Tests.Helpers;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    class TestMqttClient : MqttClientAgent
    {
        readonly ChannelWriter<object?> _eventWriter;

        public Mqtt3ClientConfiguration Config { get; }

        public TestMqttClient( ProtocolConfiguration pConfig, Mqtt3ClientConfiguration config, IMqttChannel channel, ChannelWriter<object?> eventWriter )
            : base( ( sink ) => new LowLevelMqttClient( pConfig, config, sink, channel ) )
        {
            Config = config;
            _eventWriter = eventWriter;
        }

        protected override async Task WorkLoopAsync( ChannelReader<object?> channel )
        {
            await foreach( var item in channel.ReadAllAsync() )
            {
                await _eventWriter.WriteAsync( item );
            }
        }


        protected override async ValueTask ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken )
            => await new NewApplicationMessageClosure( ReceivedMessageAsync ).HandleMessageAsync( topic, reader, size, q, retain, cancellationToken );

        async ValueTask ReceivedMessageAsync( IActivityMonitor? m, ApplicationMessage message, CancellationToken cancellationToken )
            => await Messages!.Writer.WriteAsync( message, cancellationToken );
    }
}
