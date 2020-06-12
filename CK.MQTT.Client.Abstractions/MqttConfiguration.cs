namespace CK.MQTT
{
    /// <summary>
    /// General configuration used across the protocol implementation
    /// </summary>
    public class MqttConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MqttConfiguration" /> class 
        /// </summary>
		public MqttConfiguration(
            string connectionString,
            QualityOfService maximumQoS = QualityOfService.ExactlyOnce,
            ushort keepAliveSecs = 0,
            int waitTimeoutSecs = 5,
            bool allowWildcardsInTopicFilters = true
            )
        {
            Credentials = credentials;
            ConnectionString = connectionString;
            MaximumQoS = maximumQoS;
            KeepAliveSecs = keepAliveSecs;
            WaitTimeoutSecs = waitTimeoutSecs;
            AllowWildcardsInTopicFilters = allowWildcardsInTopicFilters;
        }

        public string ConnectionString { get; set; }

        /// <summary>
        /// Maximum Quality of Service (QoS) to support
        /// Default value is AtMostOnce, which means QoS 0
        /// </summary>
		public QualityOfService MaximumQualityOfService { get; set; }

        /// <summary>
        /// Seconds to wait for the MQTT Keep Alive mechanism
        /// until a Ping packet is sent to maintain the connection alive
        /// Default value is 0 seconds, which means Keep Alive disabled
        /// </summary>
		public ushort KeepAliveSecs { get; set; }

        /// <summary>
        /// Seconds to wait for an incoming required message until the operation timeouts
        /// This value is generally used to wait for Server or Client acknowledgements
        /// Default value is 5 seconds
        /// </summary>
		public int WaitTimeoutSecs { get; set; }

        /// <summary>
        /// Determines if multi level (#)  and single level (+)
        /// wildcards are allowed on topic filters
        /// Default value is true
        /// </summary>
		public bool AllowWildcardsInTopicFilters { get; set; }

        public QualityOfService MaximumQoS { get; }
    }
}
