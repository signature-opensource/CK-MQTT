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
        /// <param name="waitTimeout">
        /// Time before the a non acknowledged packet will be sent again. When not null, it must be positive (Zero
        /// would mean that a packet is already timeout when it just has been sent).
        /// Default to <see cref="Timeout.InfiniteTimeSpan"/>.
        /// </param>
        /// <param name="storeFactory">
        /// Factory that create a store, used to store packets. When null, <see cref="MemoryStoreFactory"/> is used.
        /// </param>
        /// <param name="storeTransformer">
        /// The store transformer allow to modify packet while they are stored, or sent.
        /// When null, <see cref="DefaultStoreTransformer.Default"/> is used.
        /// </param>
        public MqttConfigurationBase(
            TimeSpan? waitTimeout = null,
            IStoreFactory? storeFactory = null,
            IStoreTransformer? storeTransformer = null )
        {
            if( waitTimeout.HasValue && waitTimeout.Value.TotalSeconds <= 0 ) throw new ArgumentException( "WaitTimeout must be positive." );
            WaitTimeout = waitTimeout ?? Timeout.InfiniteTimeSpan;
            StoreFactory = storeFactory ?? new MemoryStoreFactory();
            StoreTransformer = storeTransformer ?? DefaultStoreTransformer.Default;
        }

        /// <summary>
        /// Gets the time to wait for an incoming required message until the operation timeouts.
        /// This value is generally used to wait for Server or Client acknowledgements.
        /// </summary>
		public TimeSpan WaitTimeout { get; }

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
    }
}
