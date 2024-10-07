using System;

namespace CK.MQTT.Client;

public class DefaultClientMessageSink : MQTTMessageSink, IMQTT3ClientSink
{
    public DefaultClientMessageSink( Action<object?> events ) : base( events )
    {
    }

    public record Connected;
    public void OnConnected() => _messageWriter( new Connected() );
}
