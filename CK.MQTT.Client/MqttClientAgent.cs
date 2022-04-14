using CK.MQTT.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public class MqttClientAgent : MessageExchangerAgent<IMqtt3Client>
    {
        public MqttClientAgent( Func<IMqtt3Sink, IMqtt3Client> clientFactory ) : base( clientFactory )
        {
        }

        public Task<ConnectResult> ConnectAsync( OutgoingLastWill? lastwill = null, CancellationToken cancellationToken = default )
            => ConnectAsync( true, lastwill, cancellationToken );

        public async Task<ConnectResult> ConnectAsync( bool waitForCompletion, OutgoingLastWill? lastWill = null, CancellationToken cancellationToken = default )
        {
            Start();
            var res = await Client.ConnectAsync( lastWill, cancellationToken );
            if( !res.IsSuccess )
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
    }
}
