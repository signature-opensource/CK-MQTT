using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

// took from from MQTTnet.
// basically I stripped all the routing and locking because we are not concurrent.
// Also made the algorithm faster by removing allocations and returning asap.

namespace CK.MQTT.P2P
{
    public class SimpleTopicManager : ITopicManager
    {
        readonly Dictionary<ulong, HashSet<string>> _noWildcardSubscriptionsByTopicHash = new();
        readonly Dictionary<ulong, TopicHashMaskSubscriptions> _wildcardSubscriptionsByTopicHash = new();
        readonly Dictionary<string, MqttSubscription> _subscriptions = new();
        public bool IsFiltered( string topic )
        {
            CalculateTopicHash( topic, out ulong topicHash, out _, out _ );
            _noWildcardSubscriptionsByTopicHash.TryGetValue( topicHash, out HashSet<string>? noWildcardSubscriptions );
            if( noWildcardSubscriptions?.Contains( topic ) ?? false ) return true;

            if( noWildcardSubscriptions != null )
            {
                foreach( var subscription in noWildcardSubscriptions )
                {
                    if( MqttTopicFilterComparer.IsMatch( topic, subscription ) ) return false;
                }
            }


            foreach( var wcs in _wildcardSubscriptionsByTopicHash )
            {
                var wildcardSubscriptions = wcs.Value;
                var subscriptionHash = wcs.Key;
                var subscriptionHashMask = wildcardSubscriptions.HashMask;

                if( (topicHash & subscriptionHashMask) == subscriptionHash )
                {
                    foreach( var subscription in wildcardSubscriptions.Subscriptions )
                    {
                        if( MqttTopicFilterComparer.IsMatch( topic, subscription ) ) return false;
                    }
                }
            }
            return true;
        }


        void Subscribe( string topicFilter )
        {
            bool isNewSubscription = !_subscriptions.ContainsKey( topicFilter );

            // Add to subscriptions and maintain topic hash dictionaries
            CalculateTopicHash( topicFilter, out var topicHash, out var topicHashMask, out var hasWildcard );


            if( !isNewSubscription )
            {
                // must remove object from topic hash dictionary first
                if( hasWildcard )
                {
                    if( _wildcardSubscriptionsByTopicHash.TryGetValue( topicHash, out var subs ) )
                    {
                        subs.Subscriptions.Remove( topicFilter );
                        // no need to remove empty entry because we'll be adding subscription again below
                    }
                }
                else
                {
                    if( _noWildcardSubscriptionsByTopicHash.TryGetValue( topicHash, out var subscriptions ) )
                    {
                        subscriptions.Remove( topicFilter );
                        // no need to remove empty entry because we'll be adding subscription again below
                    }
                }
            }

            _subscriptions.Add( topicFilter, new MqttSubscription( topicFilter ) );

            // Add or re-add to topic hash dictionary
            if( hasWildcard )
            {
                if( !_wildcardSubscriptionsByTopicHash.TryGetValue( topicHash, out var subscriptions ) )
                {
                    subscriptions = new TopicHashMaskSubscriptions( topicHashMask );
                    _wildcardSubscriptionsByTopicHash.Add( topicHash, subscriptions );
                }

                subscriptions.Subscriptions.Add( topicFilter );
            }
            else
            {
                if( !_noWildcardSubscriptionsByTopicHash.TryGetValue( topicHash, out var subscriptions ) )
                {
                    subscriptions = new HashSet<string>();
                    _noWildcardSubscriptionsByTopicHash.Add( topicHash, subscriptions );
                }

                subscriptions.Add( topicFilter );
            }
        }

        void Unsubscribe( string topicFilter )
        {
            var removedSubscriptions = new List<string>();

            _subscriptions.TryGetValue( topicFilter, out var existingSubscription );

            if( existingSubscription == null ) return;
            _subscriptions.Remove( topicFilter );

            // must remove subscription object from topic hash dictionary also

            if( existingSubscription.TopicHasWildcard )
            {
                if( _wildcardSubscriptionsByTopicHash.TryGetValue( existingSubscription.TopicHash, out var subs ) )
                {
                    subs.Subscriptions.Remove( topicFilter );
                    if( subs.Subscriptions.Count == 0 )
                    {
                        _wildcardSubscriptionsByTopicHash.Remove( existingSubscription.TopicHash );
                    }
                }
            }
            else
            {
                if( _noWildcardSubscriptionsByTopicHash.TryGetValue( existingSubscription.TopicHash, out var subs ) )
                {
                    subs.Remove( topicFilter );
                    if( subs.Count == 0 )
                    {
                        _noWildcardSubscriptionsByTopicHash.Remove( existingSubscription.TopicHash );
                    }
                }
            }

            removedSubscriptions.Add( topicFilter );
        }

        static void CalculateTopicHash( string topic, out ulong resultHash, out ulong resultHashMask, out bool resultHasWildcard )
        {
            // calculate topic hash
            ulong hash = 0;
            ulong hashMaskInverted = 0;
            ulong levelBitMask = 0;
            ulong fillLevelBitMask = 0;
            var hasWildcard = false;
            byte checkSum = 0;
            var level = 0;

            var i = 0;
            while( i < topic.Length )
            {
                var c = topic[i];
                if( c == MqttTopicFilterComparer.LevelSeparator )
                {
                    // done with this level
                    hash <<= 8;
                    hash |= checkSum;
                    hashMaskInverted <<= 8;
                    hashMaskInverted |= levelBitMask;
                    checkSum = 0;
                    levelBitMask = 0;
                    ++level;
                    if( level >= 8 )
                    {
                        break;
                    }
                }
                else if( c == MqttTopicFilterComparer.SingleLevelWildcard )
                {
                    levelBitMask = 0xff;
                    hasWildcard = true;
                }
                else if( c == MqttTopicFilterComparer.MultiLevelWildcard )
                {
                    // checksum is zero for a valid topic
                    levelBitMask = 0xff;
                    // fill rest with this fillLevelBitMask
                    fillLevelBitMask = 0xff;
                    hasWildcard = true;
                    break;
                }
                else
                {
                    // The checksum should be designed to reduce the hash bucket depth for the expected
                    // fairly regularly named MQTT topics that don't differ much,
                    // i.e. "room1/sensor1"
                    //      "room1/sensor2"
                    //      "room1/sensor3"
                    // etc.
                    if( (c & 1) == 0 )
                    {
                        checkSum += (byte)c;
                    }
                    else
                    {
                        checkSum ^= (byte)(c >> 1);
                    }
                }

                ++i;
            }

            // Shift hash left and leave zeroes to fill ulong
            if( level < 8 )
            {
                hash <<= 8;
                hash |= checkSum;
                hashMaskInverted <<= 8;
                hashMaskInverted |= levelBitMask;
                ++level;
                while( level < 8 )
                {
                    hash <<= 8;
                    hashMaskInverted <<= 8;
                    hashMaskInverted |= fillLevelBitMask;
                    ++level;
                }
            }

            if( !hasWildcard )
            {
                while( i < topic.Length )
                {
                    var c = topic[i];
                    if( c == MqttTopicFilterComparer.SingleLevelWildcard || c == MqttTopicFilterComparer.MultiLevelWildcard )
                    {
                        hasWildcard = true;
                        break;
                    }

                    ++i;
                }
            }
            resultHash = hash;
            resultHashMask = ~hashMaskInverted;
            resultHasWildcard = hasWildcard;
        }

        public void Reset()
        {
            _noWildcardSubscriptionsByTopicHash.Clear();
            _wildcardSubscriptionsByTopicHash.Clear();
            _subscriptions.Clear();
        }

        public ValueTask<SubscribeReturnCode[]> SubscribeAsync( params Subscription[] subscriptions )
        {
            foreach( var subscription in subscriptions )
            {
                Subscribe( subscription.TopicFilter );
            }
            return new ValueTask<SubscribeReturnCode[]>( new SubscribeReturnCode[subscriptions.Length] );
        }

        public ValueTask UnsubscribeAsync( params string[] topicFilter )
        {
            foreach( var subscription in topicFilter )
            {
                Unsubscribe( subscription );
            }
            return new ValueTask();
        }

        public ValueTask ResetAsync()
        {
            _noWildcardSubscriptionsByTopicHash.Clear();
            _subscriptions.Clear();
            _wildcardSubscriptionsByTopicHash.Clear();
            return new ValueTask();
        }

        sealed class TopicHashMaskSubscriptions
        {
            public TopicHashMaskSubscriptions( ulong hashMask )
            {
                HashMask = hashMask;
            }

            public ulong HashMask { get; }

            public HashSet<string> Subscriptions { get; } = new HashSet<string>();
        }

        sealed class MqttSubscription
        {
            public MqttSubscription( string topic )
            {
                Topic = topic;

                CalculateTopicHash( Topic, out var hash, out var hashMask, out var hasWildcard );
                TopicHash = hash;
                TopicHashMask = hashMask;
                TopicHasWildcard = hasWildcard;
            }


            public string Topic { get; }

            public ulong TopicHash { get; }

            public ulong TopicHashMask { get; }

            public bool TopicHasWildcard { get; }
        }
    }
}
