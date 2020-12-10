using CK.Core;

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
        public MqttConfiguration( string connectionString ) => ConnectionString = connectionString;

        public string ConnectionString { get; }

        /// <summary>
        /// Gets the KeepAlive Client/Server configuration that is sent to the server in the connect packet.
        /// <para>
        /// This client will send a PingRequest packet to the server whenever it has sent nothing during this delay.
        /// Setting this to 0 disables the KeepAlive mechanism: the server will never
        /// close the connection, even if this client never sends any packet.
        /// </para>
        /// </summary>
        public ushort KeepAliveSeconds { get; init; } = 30;

        public DisconnectBehavior DisconnectBehavior { get; init; } = DisconnectBehavior.Nothing;

        public IMqttChannelFactory ChannelFactory { get; init; } = new TcpChannelFactory();
        public IActivityMonitor OnInputMonitor { get; init; } = new ActivityMonitor();

    }
}
