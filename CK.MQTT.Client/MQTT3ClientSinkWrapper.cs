namespace CK.MQTT.Client
{
    public class MQTT3ClientSinkWrapper : MQTT3SinkWrapper, IMQTT3ClientSink
    {
        readonly IMQTT3ClientSink _clientSink;

        public MQTT3ClientSinkWrapper( IMQTT3ClientSink clientSink ) : base( clientSink )
        {
            _clientSink = clientSink;
        }

        public IMQTT3Client Client { get => _clientSink.Client; set => _clientSink.Client = value; }

        public void OnConnected() => _clientSink.OnConnected();
    }
}
