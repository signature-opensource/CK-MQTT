using System.Collections.Generic;

namespace CK.MQTT.Server;

public class MQTTDemiServerConfig
{
    public HashSet<string>? ListenTo { get; set; } = null!;
    public MQTT3ConfigurationBase ImplementationConfig { get; set; } = new();
}
