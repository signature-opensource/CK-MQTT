namespace CK.MQTT
{
    /// <summary>
    /// Defines some well known values of the MQTT protocol,
    /// which are useful to access anywhere
    /// </summary>
	public class ProtocolConfiguration
    {
        public ProtocolConfiguration(
            int securePort,
            int nonSecurePort,
            byte supportedLevel,
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
        public static ProtocolConfiguration Mqtt3 => new ProtocolConfiguration( 8883, 1883, 4, "+", "#", "MQTT" );
        public int SecurePort { get; }

        public int NonSecurePort { get; }

        public byte ProtocolLevel { get; }

        /// <summary>
        /// Character that defines the single level topic wildcard, which is '+'
        /// </summary>
		public string SingleLevelTopicWildcard { get; }

        /// <summary>
        /// Character that defines the multi level topic wildcard, which is '#'
        /// </summary>
		public string MultiLevelTopicWildcard { get; }

        public string ProtocolName { get; }
    }
}
