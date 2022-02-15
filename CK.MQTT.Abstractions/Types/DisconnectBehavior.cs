namespace CK.MQTT
{
    /// <summary>
    /// An enum representing how the client behave facing a disconnection.
    /// </summary>
    public enum DisconnectBehavior
    {
        /// <summary>
        /// The client will do nothing when being disconnected.
        /// Beware, the Task representing the acks will be left pending until you call
        /// <see cref="IMqtt3Client.ConnectAsync(Core.IActivityMonitor?, MqttClientCredentials?, OutgoingLastWill?, System.Threading.CancellationToken)"/> or <see cref="IMqtt3Client.DisconnectAsync(Core.IActivityMonitor?, bool, bool, System.Threading.CancellationToken)."/>. 
        /// </summary>
        Nothing = 0,
        AutoReconnect = 1
    }
}
