using System;

namespace CK.MQTT
{
    /// <summary>
    /// Credentials used to connect a Client to a Server as part of the protocol connection.
    /// </summary>
	public class MqttClientCredentials
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MqttClientCredentials" /> class 
        /// specifying the id of client to connect
        /// </summary>
        /// <param name="clientId">Id of the client to connect. Can be null or empty.</param>
        /// <param name="cleanSession"><see langword="true"/> to require to the server to delete the previous session.</param>
        public MqttClientCredentials( string clientId, bool cleanSession )
        {
            if( clientId.Length == 0 && !cleanSession ) throw new ArgumentException( "If the Client supplies a zero-byte ClientId, the Client MUST also set CleanSession to 1 [MQTT-3.1.3-7]. http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349242" );
            MqttBinaryWriter.ThrowIfInvalidMQTTString( clientId );
            ClientId = clientId;
            CleanSession = cleanSession;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttClientCredentials" /> class 
        /// specifying the id of client to connect, the username and password
        /// for authentication
        /// </summary>
        /// <param name="clientId">Id of the client to connect</param>
        /// <param name="cleanSession"><see langword="true"/> to require to the server to delete the previous session.</param>
        /// <param name="userName">Username for authentication</param>
        /// <param name="password">Password for authentication</param>
        public MqttClientCredentials( string clientId, bool cleanSession, string? userName, string? password ) : this( clientId, cleanSession )
        {
            if( password != null ) MqttBinaryWriter.ThrowIfInvalidMQTTString( password );
            UserName = userName;
            Password = password;
        }

        /// <summary>
		/// Initializes a new instance of the <see cref="MqttClientCredentials" /> class
        /// without any <see cref="ClientId"/>: the server will provide one.
		/// </summary>
		public MqttClientCredentials() : this( clientId: string.Empty, true )
        {
        }

        /// <summary>
        /// Gets the Client's identifier.
        /// <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349242">It can also be empty.</a>
        /// </summary>
        public string ClientId { get; }

        /// <summary>
        /// User Name used for authentication.
        /// Authentication is not mandatory on MQTT and is up to the consumer of the API.
        /// </summary>
		public string? UserName { get; }

        /// <summary>
        /// Password used for authentication.
        /// Authentication is not mandatory on MQTT and is up to the consumer of the API.
        /// </summary>
		public string? Password { get; }

        /// <summary>
        /// <see langword="true"/> to require to the server to delete the previous session.
        /// </summary>
        public bool CleanSession { get; }
    }
}
