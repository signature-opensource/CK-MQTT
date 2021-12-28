namespace CK.MQTT
{
    /// <summary>
    /// Defines some well known values of the MQTT protocol, which are useful to access anywhere.
    /// </summary>
    /// <param name="SecurePort">The default port when communication are secured.</param>
    /// <param name="NonSecurePort">The default port when communication are in clear text.</param>
    /// <param name="SupportedLevel">The minimal <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349227">protocol level</a> supported.</param>
    /// <param name="SingleLevelTopicWildcard">Character that defines the single level topic wildcard, which is '+'</param>
    /// <param name="MultiLevelTopicWildcard">Character that defines the multi level topic wildcard, which is '#'</param>
    /// <param name="ProtocolName">The protocol magic string that is send in the connect packet.</param>
    public record ProtocolConfiguration(
        int SecurePort,
        int NonSecurePort,
        ProtocolLevel SupportedLevel,
        string SingleLevelTopicWildcard,
        string MultiLevelTopicWildcard,
        string ProtocolName,
        uint MaximumPacketSize = 268435455
    ) {
      
        /// <summary>
        /// Default for MQTT3.
        /// </summary>
        public static ProtocolConfiguration Mqtt3 => new( 8883, 1883, ProtocolLevel.MQTT3, "+", "#", "MQTT" );

        /// <summary>
        /// Defaults for MQTT5
        /// </summary>
        public static ProtocolConfiguration Mqtt5 => new( 8883, 1883, ProtocolLevel.MQTT5, "+", "#", "MQTT" );
    }
}
