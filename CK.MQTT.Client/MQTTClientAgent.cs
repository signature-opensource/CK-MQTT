using CK.Core;
using CK.MQTT.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CK.MQTT.Client.DefaultClientMessageSink;

namespace CK.MQTT.Client
{
    public class MQTTClientAgent : MessageExchangerAgent, IMQTT3Client
    {
        readonly DefaultClientMessageSink _sink = new();
        public MQTTClientAgent(Func<IMQTT3ClientSink, IMQTT3Client> factory) // Sink.Client is not set.
        {
            OnConnectionChange.Sync += OnConnectionChangeSync;
            factory( _sink ); //TODO: big code smell. We don't use the output there.
        }

        public IMQTT3Client Client => _sink.Client;

        protected override MQTTMessageSink MessageSink => _sink;

        void OnConnectionChangeSync( IActivityMonitor monitor, DisconnectReason e )
            => IsConnected = e == DisconnectReason.None;

        public Task<ConnectResult> ConnectAsync( OutgoingLastWill? lastwill = null, CancellationToken cancellationToken = default )
            => ConnectAsync( true, lastwill, cancellationToken );



        public async Task<ConnectResult> ConnectAsync( bool waitForCompletion, OutgoingLastWill? lastWill = null, CancellationToken cancellationToken = default )
        {
            _sink._manualCountRetry = 0;
            Start();
            var res = await Client.ConnectAsync( lastWill, cancellationToken );
            if( res.Status != ConnectStatus.Successful && res.Status != ConnectStatus.Deffered )
            {
                await StopAsync( waitForCompletion );
            }
            return res;
        }

        public bool IsConnected { get; private set; }

        public ValueTask<Task> UnsubscribeAsync( IEnumerable<string> topics )
            => Client.UnsubscribeAsync( topics.ToArray() );
        public virtual ValueTask<Task<SubscribeReturnCode>> SubscribeAsync( Subscription subscription )
            => Client.SubscribeAsync( subscription );

        public virtual ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( IEnumerable<Subscription> subscriptions )
            => Client.SubscribeAsync( subscriptions );

        public ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( params Subscription[] subscriptions )
            => SubscribeAsync( (IEnumerable<Subscription>)subscriptions );

        public ValueTask<Task> UnsubscribeAsync( params string[] topics ) => UnsubscribeAsync( (IEnumerable<string>)topics );

        public ValueTask<Task> UnsubscribeAsync( string topic )
            => Client.UnsubscribeAsync( topic );


        protected override async Task ProcessMessageAsync( IActivityMonitor m, object? item )
        {
            switch( item )
            {
                case Connected:
                    await _onConnectionChangeSender.RaiseAsync( m, DisconnectReason.None );
                    break;
                case ReconnectionFailed:
                    m.Warn( $"Reconnection failed." );
                    break;
                default:
                    await base.ProcessMessageAsync( m, item );
                    break;
            }
        }
    }
}
