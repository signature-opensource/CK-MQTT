namespace CK.MQTT
{
    /// <summary>
    /// Defines some well known values of the MQTT protocol, which are useful to access anywhere.
    /// </summary>
	public class ProtocolConfiguration
    {
        /// <summary>
        /// Instantiate a new <see cref="ProtocolConfiguration"/>.
        /// </summary>
        /// <param name="securePort">The default port when communication are secured.</param>
        /// <param name="nonSecurePort">The default port when communication are in clear text.</param>
        /// <param name="supportedLevel">The minimal <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349227">protocol level</a> supported.</param>
        /// <param name="singleLevelTopicWildcard"></param>
        /// <param name="multiLevelTopicWildcard"></param>
        /// <param name="protocolName">The protocol magic string that is send in the connect packet.</param>
        public ProtocolConfiguration(
            int securePort,
            int nonSecurePort,
            ProtocolLevel supportedLevel,
            string singleLevelTopicWildcard,
            string multiLevelTopicWildcard,
            string protocolName )
        {
            SecurePort = securePort;
            NonSecurePort = nonSecurePort;
            ProtocolLevel = supportedLevel;
            SingleLevelTopicWildcard = singleLevelTopicWildcard;
            MultiLevelTopicWildcard = multiLevelTopicWildcard;
            ProtocolName = protocolName;
        }
        /// <summary>
        /// Default for MQTT3.
        /// </summary>
        public static ProtocolConfiguration Mqtt3 => new ProtocolConfiguration( 8883, 1883, ProtocolLevel.MQTT3, "+", "#", "MQTT" );

        /// <summary>
        /// Defaults for MQTT5
        /// </summary>
        public static ProtocolConfiguration Mqtt5 => new ProtocolConfiguration( 8883, 1883, ProtocolLevel.MQTT5, "+", "#", "MQTT" );

        /// <summary>
        /// The default port when communication are secured.
        /// </summary>
        public int SecurePort { get; }

        /// <summary>
        /// The default port when communication are in clear text.
        /// </summary>
        public int NonSecurePort { get; }

        /// <summary>
        /// The minimal <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349227">protocol level</a> supported.
        /// </summary>
        public ProtocolLevel ProtocolLevel { get; }

        /// <summary>
        /// Character that defines the single level topic wildcard, which is '+'
        /// </summary>
		public string SingleLevelTopicWildcard { get; }

        /// <summary>
        /// Character that defines the multi level topic wildcard, which is '#'
        /// </summary>
		public string MultiLevelTopicWildcard { get; }

        /// <summary>
        /// The protocol magic string that is send in the connect packet.
        /// </summary>
        public string ProtocolName { get; }
    }
}
