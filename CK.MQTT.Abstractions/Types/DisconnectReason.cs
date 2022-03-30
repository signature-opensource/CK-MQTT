namespace CK.MQTT
{
    /// <summary>
    /// Reason of an MQTT Client or Server disconnection
    /// </summary>
	public enum DisconnectReason
    {
        /// <summary>
        /// Non applicable.
        /// This is used when a connexion cannot be made and, for any reason, the MQTT Client or Server
        /// must stop: such reason is not propagated to the external world.
        /// </summary>
        None,

        /// <summary>
        /// Disconnected by the remote.
        /// </summary>
		RemoteDisconnected,

        /// <summary>
        /// Explicit disconnection from this side.
        /// This could mean a protocol "Disconnect" in case of Clients or
        /// a "Stop" in case for Servers.
        /// </summary>
		UserDisconnected,

        /// <summary>
        /// A protocol error.
        /// </summary>
        ProtocolError,

        /// <summary>
        /// Disconnected because of an unexpected error on the endpoint, 
        /// This applies to a Client or a Server.
        /// </summary>
		InternalException,

        /// <summary>
        /// The disconnection is due to a Ping timeout: the server is lost.
        /// This applies to Client only.
        /// </summary>
        PingReqTimeout
    }
}
