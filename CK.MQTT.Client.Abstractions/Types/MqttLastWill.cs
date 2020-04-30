namespace CK.MQTT
{
    /// <summary>
    /// Represents the last will message sent by the Server when a Client
    /// gets disconnected unexpectedely
    /// Any disconnection except the protocol disconnection is considered unexpected
    /// </summary>
	public class LastWill
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LastWill" /> class,
        /// specifying the topic to pusblish the last will message to, the Quality of Service (QoS)
        /// to use, if the message should be sent as a retained message and also the content of the will message
        /// to publish
        /// </summary>
        /// <param name="topic">Topic to publish the last will message to</param>
        /// <param name="qualityOfService">
        /// Quality of Service (QoS) to use when publishing the last will message.
        /// See <see cref="MQTT.QualityOfService" /> for more details about the QoS meanings
        /// </param>
        /// <param name="retain">Specifies if the message should be retained or not</param>
        /// <param name="payload">Payload of the will message to publish</param>
        public LastWill( ApplicationMessage message, QualityOfService qualityOfService, bool retain )
        {
            Message = message;
            QualityOfService = qualityOfService;
            Retain = retain;
        }

        public ApplicationMessage Message { get; }

        /// <summary>
        /// Quality of Servive (QoS) associated to the will message,
        /// that will be used when the Server publishes it
        /// See <see cref="MQTT.QualityOfService" /> for more details about the QoS values
        /// </summary>
        public QualityOfService QualityOfService { get; }

        /// <summary>
        /// Determines if the message is sent as a retained message or not
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180851">fixed header</a>
        /// section for more information about retained messages
        /// </summary>
		public bool Retain { get; }
    }
}
