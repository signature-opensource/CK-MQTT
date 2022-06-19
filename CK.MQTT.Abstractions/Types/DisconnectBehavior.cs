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
        /// <see cref="IConnectedMessageExchanger.ConnectAsync(Packets.OutgoingLastWill?, System.Threading.CancellationToken)"/> or <see cref="IConnectedMessageExchanger.DisconnectAsync(bool)."/>. 
        /// </summary>
        Nothing = 0,
        AutoReconnect = 1
    }
}
