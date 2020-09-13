using System;
using System.Threading;

namespace CK.MQTT
{
    /// <summary>
    /// Configuration of a <see cref="IMqtt3Client"/>.
    /// </summary>
    public class MqttConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MqttConfiguration" /> class.
        /// </summary>
        /// <param name="connectionString">The connection string that will be used by the <see cref="IMqttChannelFactory"/>.</param>
        /// <param name="keepAlive">If the client didn't sent a packet in the given amount of time, it will send a PingRequest packet.
        /// <br/>0 to disable the KeepAlive mechanism.</param>
        /// <param name="waitTimeout">Time before the client try to resend a packet.</param>
        /// <param name="channelFactory">Factory that create a channel, used to communicated with the broker.</param>
        /// <param name="storeFactory">Factory that create a store, used to store packets.</param>
        /// <param name="storeTransformer">The store transformer allow to modify packet while they are stored, or sent.</param>
        public MqttConfiguration(
            string connectionString,
            TimeSpan keepAlive = new TimeSpan(),
            TimeSpan? waitTimeout = null,
            DisconnectBehavior disconnectBehavior = DisconnectBehavior.CancelAcksOnDisconnect,
            ushort attemptCountBeforeGivingUpPacket = 50,
            IMqttChannelFactory? channelFactory = null,
            IStoreFactory? storeFactory = null,
            IStoreTransformer? storeTransformer = null )
        {
            ConnectionString = connectionString;
            if( keepAlive.Milliseconds != 0 ) throw new ArgumentException( "MQTT KeepAlive is in seconds, but this TimeSpan does not have whole seconds." );
            if( keepAlive.TotalSeconds == 0 ) keepAlive = Timeout.InfiniteTimeSpan;
            KeepAlive = keepAlive;
            if( waitTimeout.HasValue && waitTimeout.Value.TotalSeconds <= 0 ) throw new ArgumentException( "WaitTimeout cannot be 0. It would mean that a packet is already timeout when it just has been sent." );
            WaitTimeout = waitTimeout ?? Timeout.InfiniteTimeSpan;
            DisconnectBehavior = disconnectBehavior;
            AttemptCountBeforeGivingUpPacket = attemptCountBeforeGivingUpPacket;
            ChannelFactory = channelFactory ?? new TcpChannelFactory();
            StoreFactory = storeFactory ?? new MemoryStoreFactory();
            StoreTransformer = storeTransformer ?? DefaultStoreTransformer.Default;
        }

        public string ConnectionString { get; }

        /// <summary>
        /// Seconds to wait for the MQTT Keep Alive mechanism
        /// until a Ping packet is sent to maintain the connection alive
        /// Default value is 0 seconds, which means Keep Alive disabled
        /// </summary>
        public TimeSpan KeepAlive { get; }

        /// <summary>
        /// Seconds to wait for an incoming required message until the operation timeouts
        /// This value is generally used to wait for Server or Client acknowledgements
        /// </summary>
		public TimeSpan WaitTimeout { get; }
        public DisconnectBehavior DisconnectBehavior { get; }

        //0 to disable
        public ushort AttemptCountBeforeGivingUpPacket { get; }
        public IInputLogger? InputLogger { get; set; }
        public IOutputLogger? OutputLogger { get; set; }
        public IMqttChannelFactory ChannelFactory { get; }
        public IStoreFactory StoreFactory { get; }
        public IStoreTransformer StoreTransformer { get; }
        public int ChannelsPacketCount { get; } = 32;
    }
}
