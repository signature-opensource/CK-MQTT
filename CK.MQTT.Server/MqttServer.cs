using CK.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    class MqttServer
    {
        readonly IMqttChannelListener _mqttChannelListener;
        async Task AcceptLoop()
        {
            var m = new ActivityMonitor();
            while( true )
            {
                try
                {
                    (IMqttChannel channel, string clientAddress) = await _mqttChannelListener.AcceptIncomingConnection( _stopTokenCTS.Token );
                    if( _stopTokenCTS.IsCancellationRequested )
                    {
                        m.Info( "Stop token triggered. Exiting accept loop." );
                    }
                    await AcceptClient( channel );
                }
                catch( Exception e )
                {
                    m.Error( e );
                }
            }
        }

        CancellationTokenSource _stopTokenCTS;

        async ValueTask AcceptClient( IMqttChannel channel )
        {
            ClientInstance instance = new(this, channel );
        }

    }
}
