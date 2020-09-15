namespace CK.MQTT
{
    /// <summary>
    /// Reason of an MQTT Client or Server disconnection
    /// </summary>
	public enum DisconnectedReason
    {
        /// <summary>
        /// Disconnected by the remote host.
        /// This applies only to Clients.
        /// </summary>
		RemoteDisconnected,

        /// <summary>
        /// Disconnected by the endpoint itself.
        /// This could mean a protocol Disconnect in case of Clients,
        /// a Stop in case of Servers or an explicit Dispose 
        /// of the corresponding endpoint instance.
        /// </summary>
		SelfDisconnected,

        /// <summary>
        /// A protocol error.
        /// </summary>
        ProtocolError,

        /// <summary>
        /// Disconnected because of an unexpected error on the endpoint, 
        /// This applies to a Client or a Server.
        /// </summary>
		UnspecifiedError,


        PingReqTimeout
    }
}
