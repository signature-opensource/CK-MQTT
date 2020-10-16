using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Client.Tests
{
    static class TestHelper
    {
        public static (IMqtt3Client client, TestChannel testChannel) CreateMQTT3Client( MessageHandlerDelegate messageHandlerDelegate )
        {
            TestChannel channel = new TestChannel();
            IMqtt3Client client = MqttClient.CreateMQTT3Client(
                new MqttConfiguration( "", waitTimeoutMilliseconds: int.MaxValue, channelFactory: new TestChannelFactory( channel ) ),
                messageHandlerDelegate
            );
            return (client, channel);
        }
    }
}
