using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Server
{
    class MqttServerConfiguration : MqttConfigurationBase
    {
        public IServerStoreFactory StoreFactory { get; init; }
    }
}
