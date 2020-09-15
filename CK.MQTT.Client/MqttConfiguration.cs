using System;
using System.Threading;

namespace CK.MQTT
{
    /// <summary>
    /// Configuration of a <see cref="IMqtt3Client"/>.
    /// </summary>
    public class MqttConfiguration : MqttConfigurationBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MqttConfiguration" /> class.
        /// </summary>
        /// <param name="connectionString">The connection string that will be used by the <see cref="IMqttChannelFactory"/>.</param>
        /// <param name="keepAliveSeconds">
        /// This client will send a PingRequest packet to the server whenever it has sent nothing during this delay.
        /// The Server knows this value and setting this to 0 disables the KeepAlive mechanism: the server will never
        /// close the connection, even if this client never sends any packet.
        /// </param>
        /// <param name="waitTimeoutMilliseconds">
        /// Time to wait before a non acknowledged packet is resent. Must be greater than 20 ms.
        /// Defaults to 5 seconds.
        /// </param>
        /// <param name="attemptCountBeforeGivingUpPacket">See <see cref="MqttConfigurationBase.AttemptCountBeforeGivingUpPacket"/>.</param>
        /// <param name="channelFactory">Factory that create a channel, used to communicated with the broker.</param>
        /// <param name="storeFactory">Factory that create a store, used to store packets.</param>
        /// <param name="storeTransformer">The store transformer allow to modify packet while they are stored, or sent.</param>
        public MqttConfiguration(
            string connectionString,
            ushort keepAliveSeconds = 0,
            int waitTimeoutMilliseconds = 5_000,
            ushort attemptCountBeforeGivingUpPacket = 50,
            DisconnectBehavior disconnectBehavior = DisconnectBehavior.CancelAcksOnDisconnect,
            IMqttChannelFactory? channelFactory = null,
            IStoreFactory? storeFactory = null,
            IStoreTransformer? storeTransformer = null )
            : base( waitTimeoutMilliseconds, attemptCountBeforeGivingUpPacket, storeFactory, storeTransformer )
        {
            ConnectionString = connectionString;
            KeepAliveSeconds = keepAliveSeconds;
            DisconnectBehavior = disconnectBehavior;
            ChannelFactory = channelFactory ?? new TcpChannelFactory();
        }

        public string ConnectionString { get; }

        /// <summary>
        /// Gets the KeepAlive Client/Server configuration that is sent to the server in the connect packet.
        /// <para>
        /// This client will send a PingRequest packet to the server whenever it has sent nothing during this delay.
        /// Setting this to 0 disables the KeepAlive mechanism: the server will never
        /// close the connection, even if this client never sends any packet.
        /// </para>
        /// </summary>
        public ushort KeepAliveSeconds { get; }

        public DisconnectBehavior DisconnectBehavior { get; }

        public IMqttChannelFactory ChannelFactory { get; }

    }
}
