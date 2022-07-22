using CK.MQTT.LowLevelClient.Time;
using CK.MQTT.Stores;
using System;

namespace CK.MQTT
{
    /// <summary>
    /// Base configuration that applies to a server as well as a MQTT client.
    /// </summary>
    public class Mqtt3ConfigurationBase
    {
        int _waitTimeoutMilliseconds = 5_000;

        /// <summary>
        /// Time to wait before a non acknowledged packet is resent.
        /// Defaults to 5 seconds (and always greater than 20 ms).
        /// To disable this (but please be sure to understand the consequences), use
        /// the <see cref="int.MaxValue"/> special value.
        /// </summary>
        public int WaitTimeoutMilliseconds
        {
            get => _waitTimeoutMilliseconds;
            set
            {
                if( value <= 20 ) throw new ArgumentException( "waitTimeoutMilliseconds must be greater than 20." );
                _waitTimeoutMilliseconds = value;
            }
        }

        /// <summary>
        /// Gets the store transformer to use when sending packets.
        /// The default one set the dup flag when resending packets.
        /// </summary>
        public IStoreTransformer StoreTransformer { get; init; } = DefaultStoreTransformer.Default;

        /// <summary>
        /// Gets the capacity of the outgoing channel.
        /// Using a bounded channel enables back pressure handling.
        /// Defaults to 32.
        /// </summary>
        public int OutgoingPacketsChannelCapacity { get; set; } = 32;

        /// <summary>
        /// Initial capacity of the ID Store. May grow bigger.
        /// </summary>
        public ushort IdStoreStartCount { get; set; } = 32;

        /// <summary>
        /// Gets the maximal number of retries to send the same packet
        /// before giving up.
        /// <para>
        /// Setting it to 0 disables this check but this should be avoided (the default
        /// is 50) since this is a simple (yet effective) "poisonous message" detection:
        /// this gracefully handles a firewall that blocks a packet or a remote that
        /// repeatedly fails on a packet, avoiding such poisonous packets to remain in
        /// the system.
        /// </para>
        /// </summary>
        public ushort AttemptCountBeforeGivingUpPacket { get; set; } = 50;

        public ITimeUtilities TimeUtilities { get; init; } = new DefaultTimeUtilities();

        public int StoreFullWaitTimeoutMs { get; set; } = 500;

    }
}
