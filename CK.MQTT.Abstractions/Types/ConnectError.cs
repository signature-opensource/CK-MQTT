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
        RemoteDisconnected,
        InternalException,
        Connection_Cancelled,
        ProtocolError_InvalidConnackState,
        ProtocolError_UnexpectedConnectResponse,
        ProtocolError_UnknownReturnCode,
        ProtocolError_SessionNotFlushed,
        ProtocolError_IncompleteResponse,
        /// <summary>
        /// Other reasons...
        /// </summary>
        Other = byte.MaxValue
    }
}
