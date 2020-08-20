namespace CK.MQTT
{
    /// <summary>
    /// Reason of an MQTT Client or Server disconnection
    /// </summary>
	public enum DisconnectedReason
    {
        /// <summary>
        /// Disconnected by the remote host
        /// </summary>
        /// <remarks>
        /// This reason is used only on Client disconnections
        /// </remarks>
		RemoteDisconnected,
        /// <summary>
        /// Disconnected by the endpoint itself
        /// This could mean a protocol Disconnect in case of Clients,
        /// a Stop in case of Servers or an explicit Dispose 
        /// of the corresponding endpoint instance
        /// </summary>
		SelfDisconnected,
        /// <summary>
        /// A protocol error.
        /// </summary>
        ProtocolError,
        /// <summary>
        /// Disconnected because of an unexpected error on the endpoint, 
        /// being this the Client or Server
        /// </summary>
		UnspecifiedError
    }
}
