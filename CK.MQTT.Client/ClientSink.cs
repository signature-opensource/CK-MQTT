namespace CK.MQTT.Client
{
    public class DefaultClientMessageSink : MQTTMessageSink, IMQTT3ClientSink
    {
        internal int _manualCountRetry;
        public IMQTT3Client Client { get; set; } = null!; //set by the client.

        public record Connected;
        public void OnConnected() => Events.TryWrite( new Connected() );
    }
}
