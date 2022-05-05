using CK.MQTT.Server.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Tests
{
    class Helpers
    {
        // Create demi server
        public static MqttDemiServer CreateDemiServer( int port )
        {
            var server = new MqttServer( port );
            server.Start();
            return server;
        }


        //create client
    }
}
