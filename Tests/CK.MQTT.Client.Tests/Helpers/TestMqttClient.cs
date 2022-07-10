using CK.Core;
using CK.MQTT.Client.Tests.Helpers;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
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
            OnMessage.Simple.Async += Simple_Async;
        }

        async Task Simple_Async( IActivityMonitor monitor, ApplicationMessage message )
        {
            await Events!.Writer.WriteAsync( message, default );
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
    }
}
