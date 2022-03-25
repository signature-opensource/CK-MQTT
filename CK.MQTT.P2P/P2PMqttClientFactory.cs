using CK.MQTT.P2P;

namespace CK.MQTT
{
    public class P2PMqttClientFactory
    {
        internal P2PMqttClientFactory() { }
    }

    public static class P2PMqttClient
    {
        /// <summary>
        /// Factory to create a MQTT Client.
        /// </summary>
        public static P2PMqttClientFactory Factory { get; } = new P2PMqttClientFactory();
    }

    public static class MqttClientFactories
    {

        public static P2PClient CreateMQTTClient( this P2PMqttClientFactory? factory, IMqtt5ServerClientSink sink, P2PMqttConfiguration config )
            => new( sink, config );
    }

}
