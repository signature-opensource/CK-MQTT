using System.Threading.Channels;

namespace CK.MQTT.Client
{
    public class DefaultClientMessageSink : MQTTMessageSink, IMQTT3ClientSink
    {
        public DefaultClientMessageSink( ChannelWriter<object?> events ) : base( events )
        {
        }

        public record Connected;
        public void OnConnected() => _events.TryWrite( new Connected() );
    }
}
