namespace CK.MQTT
{
    /// <summary>
    /// An enum representing how the client behave facing a disconnection.
    /// </summary>
    public enum DisconnectBehavior
    {
        /// <summary>
        /// The client will do nothing when being disconnected.
        /// Beware, the code using the library should reconnect the client or the ack Task will be waiting for ever.
        /// </summary>
        Nothing = 0,
        AutoReconnect = 1,
        CancelAcksOnDisconnect = 2,
        AutoReconnectAndCancelAcks = AutoReconnect | CancelAcksOnDisconnect
    }
}
