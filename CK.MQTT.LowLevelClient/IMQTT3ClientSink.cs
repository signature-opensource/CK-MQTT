namespace CK.MQTT.Client
{
    public interface IMQTT3ClientSink : IMQTT3Sink
    {
        /// <summary>
        /// Called when the client is successfuly connected.
        /// </summary>
        void OnConnected();
    }
}
