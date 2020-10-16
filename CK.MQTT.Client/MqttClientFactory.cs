using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT
{
    public class MqttClientFactory
    {
        public IMqtt3Client CreateMQTT3Client( MqttConfiguration config, MessageHandlerDelegate messageHandler )
            => new MqttClient( ProtocolConfiguration.Mqtt3, config, messageHandler );
        public IMqtt5Client CreateMQTT5Client( MqttConfiguration config, MessageHandlerDelegate messageHandler )
            => new MqttClient( ProtocolConfiguration.Mqtt5, config, messageHandler );
        public IMqttClient CreateMQTTClient( MqttConfiguration config, MessageHandlerDelegate messageHandler )
            => new MqttClient( ProtocolConfiguration.Mqtt5, config, messageHandler );
    }
}
