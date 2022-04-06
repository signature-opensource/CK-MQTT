using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// "heavily inspired" from MQTTnet.
// basically we stripped all the routing, because we don't need it,
// and locking because we are not concurrent.

namespace CK.MQTT.P2P
{
    public class TopicFilter : ITopicFilter
    {
        readonly MqttServerEventContainer _eventContainer;
        readonly MqttNetSourceLogger _logger;
        readonly MqttServerOptions _options;
        readonly MqttPacketFactories _packetFactories = new MqttPacketFactories();

        readonly MqttRetainedMessagesManager _retainedMessagesManager;

        readonly Dictionary<string, MqttSession> _sessions = new Dictionary<string, MqttSession>( 4096 );

        readonly object _sessionsManagementLock = new object();
        readonly HashSet<MqttSession> _subscriberSessions = new HashSet<MqttSession>();

        public MqttClientSessionsManager(
            MqttServerOptions options,
            MqttRetainedMessagesManager retainedMessagesManager,
            MqttServerEventContainer eventContainer )
        {
            if( logger == null )
            {
                throw new ArgumentNullException( nameof( logger ) );
            }

            _logger = logger.WithSource( nameof( MqttClientSessionsManager ) );

            _options = options ?? throw new ArgumentNullException( nameof( options ) );
            _retainedMessagesManager = retainedMessagesManager ?? throw new ArgumentNullException( nameof( retainedMessagesManager ) );
            _eventContainer = eventContainer ?? throw new ArgumentNullException( nameof( eventContainer ) );
        }

        public CheckSubscriptionsResult CheckSubscriptions( string topic, ulong topicHash, MqttQualityOfServiceLevel applicationMessageQoSLevel, string senderClientId )
        {
            var possibleSubscriptions = new List<MqttSubscription>();

            // Check for possible subscriptions. They might have collisions but this is fine.
            _subscriptionsLock.Wait();
            try
            {
                if( _noWildcardSubscriptionsByTopicHash.TryGetValue( topicHash, out var noWildcardSubscriptions ) )
                {
                    possibleSubscriptions.AddRange( noWildcardSubscriptions.ToList() );
                }

                foreach( var wcs in _wildcardSubscriptionsByTopicHash )
                {
                    var wildcardSubscriptions = wcs.Value;
                    var subscriptionHash = wcs.Key;
                    var subscriptionHashMask = wildcardSubscriptions.HashMask;

                    if( (topicHash & subscriptionHashMask) == subscriptionHash )
                    {
                        possibleSubscriptions.AddRange( wildcardSubscriptions.Subscriptions.ToList() );
                    }
                }
            }
            finally
            {
                _subscriptionsLock.Release();
            }

            // The pre check has evaluated that nothing is subscribed.
            // If there were some possible candidates they get checked below
            // again to avoid collisions.
            if( possibleSubscriptions.Count == 0 )
            {
                return CheckSubscriptionsResult.NotSubscribed;
            }

            var senderIsReceiver = string.Equals( senderClientId, _session.Id );
            var maxQoSLevel = -1; // Not subscribed.

            HashSet<uint> subscriptionIdentifiers = null;
            var retainAsPublished = false;

            foreach( var subscription in possibleSubscriptions )
            {
                if( subscription.NoLocal && senderIsReceiver )
                {
                    // This is a MQTTv5 feature!
                    continue;
                }

                if( MqttTopicFilterComparer.Compare( topic, subscription.Topic ) != MqttTopicFilterCompareResult.IsMatch )
                {
                    continue;
                }

                if( subscription.RetainAsPublished )
                {
                    // This is a MQTTv5 feature!
                    retainAsPublished = true;
                }

                if( (int)subscription.GrantedQualityOfServiceLevel > maxQoSLevel )
                {
                    maxQoSLevel = (int)subscription.GrantedQualityOfServiceLevel;
                }

                if( subscription.Identifier > 0 )
                {
                    if( subscriptionIdentifiers == null )
                    {
                        subscriptionIdentifiers = new HashSet<uint>();
                    }

                    subscriptionIdentifiers.Add( subscription.Identifier );
                }
            }

            if( maxQoSLevel == -1 )
            {
                return CheckSubscriptionsResult.NotSubscribed;
            }

            var result = new CheckSubscriptionsResult
            {
                IsSubscribed = true,
                RetainAsPublished = retainAsPublished,
                SubscriptionIdentifiers = subscriptionIdentifiers?.ToList() ?? EmptySubscriptionIdentifiers,

                // Start with the same QoS as the publisher.
                QualityOfServiceLevel = applicationMessageQoSLevel
            };

            // Now downgrade if required.
            //
            // If a subscribing Client has been granted maximum QoS 1 for a particular Topic Filter, then a QoS 0 Application Message matching the filter is delivered
            // to the Client at QoS 0. This means that at most one copy of the message is received by the Client. On the other hand, a QoS 2 Message published to
            // the same topic is downgraded by the Server to QoS 1 for delivery to the Client, so that Client might receive duplicate copies of the Message.

            // Subscribing to a Topic Filter at QoS 2 is equivalent to saying "I would like to receive Messages matching this filter at the QoS with which they were published".
            // This means a publisher is responsible for determining the maximum QoS a Message can be delivered at, but a subscriber is able to require that the Server
            // downgrades the QoS to one more suitable for its usage.
            if( maxQoSLevel < (int)applicationMessageQoSLevel )
            {
                result.QualityOfServiceLevel = (MqttQualityOfServiceLevel)maxQoSLevel;
            }

            return result;
        }
    }
}
