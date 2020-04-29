using CK.MQTT.Sdk;

namespace CK.MQTT
{
    /// <summary>
    /// Defines some well known values of the MQTT protocol,
    /// which are useful to access anywhere
    /// </summary>
	public class MqttProtocol
    {
        /// <summary>
        /// Default port for using secure communication on MQTT, which is 8883
        /// </summary>
		public const int DefaultSecurePort = 8883;

        /// <summary>
        /// Default port for using non secure communication on MQTT, which is 1883
        /// </summary>
		public const int DefaultNonSecurePort = 1883;

        /// <summary>
        /// Supported protocol level for the version 3.1.1 of the protocol, which is level 4.
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180843">Protocol Level</a>
        /// for more details about this value
        /// </summary>
		public const int SupportedLevel = 4;

        /// <summary>
        /// Character that defines the single level topic wildcard, which is '+'
        /// </summary>
		public const string SingleLevelTopicWildcard = "+";

        /// <summary>
        /// Character that defines the multi level topic wildcard, which is '#'
        /// </summary>
		public const string MultiLevelTopicWildcard = "#";

        /// <summary>
        /// Maximum length supported  for the Client Id, which is 65535 bytes.
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180844">Client Identifier</a>
        /// for more details.
        /// </summary>
        public const int ClientIdMaxLength = 65535;

        public const string Name = "MQTT";

        public static readonly int NameLength = Name.Length + StringPrefixLength;

        public const int MaxIntegerLength = 65535;

        public const int StringPrefixLength = 2;

        public const int PacketTypeLength = 1;

        public static MqttEncoder Encoding => MqttEncoder.Default;
    }
}
