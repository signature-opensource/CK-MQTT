using CK.Core;
using CK.MQTT.Client.Tests.Helpers;
using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.ConstrainedExecution;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public class TestMqttClient : MqttClientAgent
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
            var copyChannel = Channel.CreateUnbounded<object?>();
            Task task;
            try
            {

                task = base.WorkLoopAsync( copyChannel.Reader );
                await foreach( var item in channel.ReadAllAsync() )
                {
                    await copyChannel.Writer.WriteAsync( item );
                    await _eventWriter.WriteAsync( item );
                }
            }
            finally
            {
                copyChannel.Writer.Complete();
            }
            await task;
        }


        protected override async ValueTask ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken )
            => await new NewApplicationMessageClosure( ReceivedMessageAsync ).HandleMessageAsync( topic, reader, size, q, retain, cancellationToken );

        async ValueTask ReceivedMessageAsync( IActivityMonitor? m, ApplicationMessage message, CancellationToken cancellationToken )
            => await Events!.Writer.WriteAsync( message, cancellationToken );
    }
}
