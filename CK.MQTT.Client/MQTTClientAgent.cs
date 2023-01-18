using CK.Core;
using CK.MQTT.Client.Middleware;
using CK.MQTT.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public class MQTTClientAgent : MessageExchangerAgent, IMQTT3Client
    {
        readonly IMQTT3Client _client;

        public MQTTClientAgent( IMQTT3Client client, MessageWorker messageWorker ) : base( client, messageWorker )
        {
            messageWorker.Middlewares.Add( new HandleConnected( _onConnectionChangeSender ) );
            OnConnectionChange.Sync += OnConnectionChangeSync;
            _client = client;
        }

        void OnConnectionChangeSync( IActivityMonitor monitor, DisconnectReason e )
            => IsConnected = e == DisconnectReason.None;

        public Task<ConnectResult> ConnectAsync( bool cleanSession, CancellationToken cancellationToken = default )
            => ConnectAsync( cleanSession, true, null, cancellationToken );
        public Task<ConnectResult> ConnectAsync( bool cleanSession, OutgoingLastWill? lastwill, CancellationToken cancellationToken = default )
            => ConnectAsync( cleanSession, true, lastwill, cancellationToken );



        public Task<ConnectResult> ConnectAsync( bool cleanSession, bool waitForCompletion, OutgoingLastWill? lastWill = null, CancellationToken cancellationToken = default )
            => _client.ConnectAsync( cleanSession, lastWill, cancellationToken );

        public bool IsConnected { get; private set; }

        public ValueTask<Task> UnsubscribeAsync( IEnumerable<string> topics )
            => _client.UnsubscribeAsync( topics.ToArray() );
        public virtual ValueTask<Task<SubscribeReturnCode>> SubscribeAsync( Subscription subscription )
            => _client.SubscribeAsync( subscription );

        public virtual ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( IEnumerable<Subscription> subscriptions )
            => _client.SubscribeAsync( subscriptions );

        public ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( params Subscription[] subscriptions )
            => SubscribeAsync( (IEnumerable<Subscription>)subscriptions );

        public ValueTask<Task> UnsubscribeAsync( params string[] topics ) => UnsubscribeAsync( (IEnumerable<string>)topics );

        public ValueTask<Task> UnsubscribeAsync( string topic )
            => _client.UnsubscribeAsync( topic );
    }
}
