using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public class MQTT3ClientSinkWrapper : MQTT3SinkWrapper, IMQTT3ClientSink
    {
        readonly IMQTT3ClientSink _clientSink;

        public MQTT3ClientSinkWrapper( IMQTT3ClientSink clientSink ) : base( clientSink )
        {
            _clientSink = clientSink;
        }

        public IMQTT3Client Client { get => _clientSink.Client; set => _clientSink.Client = value; }

        public void OnConnected() => _clientSink.OnConnected();

        public IMQTT3ClientSink.ManualConnectRetryBehavior OnFailedManualConnect( ConnectResult connectResult ) => _clientSink.OnFailedManualConnect( connectResult );

        public ValueTask<bool> OnReconnectionFailedAsync( ConnectResult result ) => _clientSink.OnReconnectionFailedAsync( result );
    }
}
