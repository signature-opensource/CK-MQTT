using System;

namespace CK.MQTT
{
    /// <summary>
    /// Configuration of a <see cref="ILowLevelMqtt3Client"/>.
    /// </summary>
    public class Mqtt3ClientConfiguration : Mqtt3ConfigurationBase
    {
        private DisconnectBehavior _disconnectBehavior = DisconnectBehavior.Nothing;

        /// <summary>
        /// Initializes a new instance of the <see cref="Mqtt3ClientConfiguration" /> class.
        /// </summary>
        /// <param name="connectionString">The connection string that will be used by the <see cref="IMqttChannelFactory"/>.</param>
        public Mqtt3ClientConfiguration(string connectionString ) => ConnectionString = connectionString;

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

        public DisconnectBehavior DisconnectBehavior
        {
            get => _disconnectBehavior;
            init
            {
                if( !Enum.IsDefined( value ) ) throw new ArgumentOutOfRangeException( nameof( value ) );
                _disconnectBehavior = value;
            }
        }

        public IMqttChannelFactory ChannelFactory { get; init; } = new TcpChannelFactory();

        public MqttClientCredentials? Credentials { get; set; }

    }
}
