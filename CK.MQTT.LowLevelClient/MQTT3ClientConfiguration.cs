using System;

namespace CK.MQTT
{
    /// <summary>
    /// Configuration of a <see cref="IConnectedMessageSender"/>.
    /// </summary>
    public class MQTT3ClientConfiguration : MQTT3ConfigurationBase
    {
        /// <summary>
        /// Gets the KeepAlive Client/Server configuration that is sent to the server in the connect packet.
        /// <para>
        /// This client will send a PingRequest packet to the server whenever it has sent nothing during this delay.
        /// Setting this to 0 disables the KeepAlive mechanism: the server will never
        /// close the connection, even if this client never sends any packet.
        /// </para>
        /// </summary>
        public ushort KeepAliveSeconds { get; init; } = 30;

        private DisconnectBehavior _disconnectBehavior = DisconnectBehavior.Nothing;
        public DisconnectBehavior DisconnectBehavior
        {
            get => _disconnectBehavior;
            init
            {
                if( !Enum.IsDefined( value ) ) throw new ArgumentOutOfRangeException( nameof( value ) );
                _disconnectBehavior = value;
            }
        }

        public /*required*/ string ConnectionString { get; init; }

        /// <summary>
        /// Gets the Client's identifier.
        /// <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349242">It can also be empty.</a>
        /// </summary>
        public string ClientId { get; init; } = "";

        /// <summary>
        /// User Name used for authentication.
        /// Authentication is not mandatory on MQTT and is up to the consumer of the API.
        /// </summary>
        public string? UserName { get; init; }

        /// <summary>
        /// Password used for authentication.
        /// Authentication is not mandatory on MQTT and is up to the consumer of the API.
        /// </summary>
        public string? Password { get; init; }

        public ManualConnectBehavior ManualConnectBehavior { get; set; }

    }
}
