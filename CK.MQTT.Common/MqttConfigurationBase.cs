using System;
using System.Threading;

namespace CK.MQTT
{
    /// <summary>
    /// Base configuration that applies to a server as well as a MQTT client.
    /// </summary>
    public class MqttConfigurationBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MqttConfiguration" /> class.
        /// </summary>
        /// <param name="waitTimeoutMilliseconds">
        /// Time to wait before a non acknowledged packet is resent. Must be greater than 20 ms.
        /// Defaults to 5 seconds.
        /// </param>
        /// <param name="attemptCountBeforeGivingUpPacket">See <see cref="AttemptCountBeforeGivingUpPacket"/>.</param>
        /// <param name="storeFactory">
        /// Factory that create a store, used to store packets. When null, <see cref="MemoryStoreFactory"/> is used.
        /// </param>
        /// <param name="storeTransformer">
        /// The store transformer allow to modify packet while they are stored, or sent.
        /// When null, <see cref="DefaultStoreTransformer.Default"/> is used.
        /// </param>
        /// <param name="outgoingPacketsChannelCapacity">See <see cref="OutgoingPacketsChannelCapacity"/>.</param>
        protected MqttConfigurationBase(
            int waitTimeoutMilliseconds = 5_000,
            ushort attemptCountBeforeGivingUpPacket = 50,
            IStoreFactory? storeFactory = null,
            IStoreTransformer? storeTransformer = null,
            int outgoingPacketsChannelCapacity = 32 )
        {
            if( waitTimeoutMilliseconds <= 20 ) throw new ArgumentException( "waitTimeoutMilliseconds must be greater than 20." );
            WaitTimeoutMilliseconds = waitTimeoutMilliseconds;
            StoreFactory = storeFactory ?? new MemoryStoreFactory();
            StoreTransformer = storeTransformer ?? DefaultStoreTransformer.Default;
            OutgoingPacketsChannelCapacity = outgoingPacketsChannelCapacity;
            AttemptCountBeforeGivingUpPacket = attemptCountBeforeGivingUpPacket;
        }

        /// <summary>
        /// Time to wait before a non acknowledged packet is resent.
        /// Defaults to 5 seconds (and always greater than 20 ms).
        /// To disable this (but please be sure to understand the consequences), use
        /// the <see cref="int.MaxValue"/> special value.
        /// </summary>
		public int WaitTimeoutMilliseconds { get; }

        /// <summary>
        /// Gets or sets the input logger to use. This can be changed at any moment.
        /// When null (the default), logging overhead is practically null.
        /// </summary>
        public IInputLogger? InputLogger { get; set; }

        /// <summary>
        /// Gets or sets the output logger to use. This can be changed at any moment.
        /// When null (the default), logging overhead is practically null.
        /// </summary>
        public IOutputLogger? OutputLogger { get; set; }

        /// <summary>
        /// Gets the store factory to use.
        /// </summary>
        public IStoreFactory StoreFactory { get; }

        /// <summary>
        /// Gets the store transformer to use.
        /// </summary>
        public IStoreTransformer StoreTransformer { get; }

        /// <summary>
        /// Gets the capacity of the outgoing channel.
        /// Using a bounded channel enables back pressure handling.
        /// Defaults to 32.
        /// </summary>
        public int OutgoingPacketsChannelCapacity { get; }

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
        public ushort AttemptCountBeforeGivingUpPacket { get; }


    }
}
