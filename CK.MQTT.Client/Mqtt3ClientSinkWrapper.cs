using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public class Mqtt3ClientSinkWrapper : Mqtt3SinkWrapper, IMqtt3ClientSink
    {
        readonly IMqtt3ClientSink _clientSink;

        public Mqtt3ClientSinkWrapper( IMqtt3ClientSink clientSink ) : base( clientSink )
        {
            _clientSink = clientSink;
        }

        public IMqtt3Client Client { get => _clientSink.Client; set => _clientSink.Client = value; }

        public void OnConnected() => _clientSink.OnConnected();

        public IMqtt3ClientSink.ManualConnectRetryBehavior OnFailedManualConnect( ConnectResult connectResult ) => _clientSink.OnFailedManualConnect( connectResult );

        public ValueTask<bool> OnReconnectionFailedAsync( ConnectResult result ) => _clientSink.OnReconnectionFailedAsync( result );
    }
}
