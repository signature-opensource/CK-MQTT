using System.Collections.Generic;

namespace CK.MQTT.Server
{
    public class MQTTDemiServerConfig
    {
        public IEnumerable<string> ListenTo { get; set; } = new[] { "tcp:1883" };
        public Mqtt3ConfigurationBase ImplementationConfig { get; set; } = new();
    }
}
