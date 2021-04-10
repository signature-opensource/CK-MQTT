//using CK.Core;
//using System;
//using System.Threading.Tasks;

//namespace CK.MQTT.Server
//{
//    class MqttServer
//    {
//        IMqttChannelListener _mqttChannelListener;
//        Task AcceptLoop()
//        {
//            var m = new ActivityMonitor();
//            while( true )
//            {
//                try
//                {
//                    //var channel = await _mqttChannelListener.AcceptIncomingConnection();
//                }
//                catch( Exception e )
//                {
//                    m.Error( e );
//                }
//            }
//        }

//        async ValueTask AcceptClient( IMqttChannel channel )
//        {
//            var instance = new ClientInstance( channel );
            
//        }

//    }
//}
