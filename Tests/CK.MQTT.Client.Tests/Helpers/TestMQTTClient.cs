using CK.Core;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public class TestMQTTClient : MQTTClientAgent
    {
        readonly ChannelWriter<object?> _eventWriter;

        public MQTT3ClientConfiguration Config { get; }

        public TestMQTTClient( ProtocolConfiguration pConfig, MQTT3ClientConfiguration config, IMQTTChannel channel, ChannelWriter<object?> eventWriter )
            : base( ( sink ) => new LowLevelMQTTClient( pConfig, config, sink, channel ) )
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
                    if( item is VolatileApplicationMessage msg )
                    {
                        var buffer = msg.Message.Payload.ToArray();
                        var appMessage = new VolatileApplicationMessage(
                            new ApplicationMessage( msg.Message.Topic, buffer, msg.Message.QoS, msg.Message.Retain ), new DisposableComposite()
                        );
                        await copyChannel.Writer.WriteAsync( item );
                        await _eventWriter.WriteAsync( appMessage );
                    }
                    else
                    {
                        await copyChannel.Writer.WriteAsync( item );
                        await _eventWriter.WriteAsync( item );
                    }
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
