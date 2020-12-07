using System;

namespace CK.MQTT
{
    /// <summary>
    /// Base configuration that applies to a server as well as a MQTT client.
    /// </summary>
    public class MqttConfigurationBase
    {
        readonly int _waitTimeoutMilliseconds = 5_000;
        /// <summary>
        /// Time to wait before a non acknowledged packet is resent.
        /// Defaults to 5 seconds (and always greater than 20 ms).
        /// To disable this (but please be sure to understand the consequences), use
        /// the <see cref="int.MaxValue"/> special value.
        /// </summary>
		public int WaitTimeoutMilliseconds
        {
            get => _waitTimeoutMilliseconds;
            init
            {
                if( value <= 20 ) throw new ArgumentException( "waitTimeoutMilliseconds must be greater than 20." );
                _waitTimeoutMilliseconds = value;
            }
        }

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
        public IStoreFactory StoreFactory { get; init; } = new MemoryStoreFactory();

        /// <summary>
        /// Gets the store transformer to use.
        /// </summary>
        public IStoreTransformer StoreTransformer { get; init; } = DefaultStoreTransformer.Default;

        /// <summary>
        /// Gets the capacity of the outgoing channel.
        /// Using a bounded channel enables back pressure handling.
        /// Defaults to 32.
        /// </summary>
        public int OutgoingPacketsChannelCapacity { get; init; } = 32;

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

        public IDelayHandler DelayHandler { get; init; } = MqttDelayHandler.Default;

        public IStopwatchFactory StopwatchFactory { get; init; } = new MqttStopwatchFactory();
    }
}
