using CK.MQTT.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.P2P
{
    public class P2PMqttConfiguration : Mqtt3ClientConfiguration
    {
        public P2PMqttConfiguration() : base( "" )
        {
        }

        public IStoreFactory StoreFactory { get; init; } = new MemoryStoreFactory();
    }
}
