using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Server
{
    abstract class MqttInternalServer : MqttDemiServer
    {
        protected MqttInternalServer( Mqtt3ConfigurationBase config, IMqttChannelFactory channelFactory, IStoreFactory storeFactory ) : base( config, channelFactory, storeFactory )
        {
        }
    }
}
