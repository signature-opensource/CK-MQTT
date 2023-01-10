namespace CK.MQTT
{
    /// <summary>
    /// An error code used if the client did not receive the CONNACK packet, or if the server refused the connexion.
    /// </summary>
    public enum ConnectError
    {
        /// <summary>
        /// No Error.
        /// </summary>
        None = 0,
        /// <summary>
        /// The server answered a ConnAck which telling us why the server refused the connexion.
        /// </summary>
        SeeReturnCode,
        /// <summary>
        /// The server didn't answered in the given time.
        /// </summary>
        Timeout,
        /// <summary>
        /// The server closed the connection.
        /// </summary>
        Disconnected,
        /// <summary>
        /// Internal exception while trying to connect.
        /// </summary>
        InternalException,
        /// <summary>
        /// Connection has been cancelled by the provided <see cref="System.Threading.CancellationToken"/>.
        /// </summary>
        UserCancelled,
        /// <summary>
        /// Protocol error.
        /// </summary>
        ProtocolError
    }
}
