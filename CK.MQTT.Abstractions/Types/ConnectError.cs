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
        /// <summary>
        /// Internal exception while trying to connect.
        /// </summary>
        InternalException,
        /// <summary>
        /// Connection has been cancelled by the provided <see cref="System.Threading.CancellationToken"/>.
        /// </summary>
        Connection_Cancelled,
        /// <summary>
        /// Protocol error: server responded with an invalid connect state.
        /// </summary>
        ProtocolError_InvalidConnackState,
        /// <summary>
        /// Protocol error: server responded with something else than ConnectAck.
        /// </summary>
        ProtocolError_UnexpectedConnectResponse,
        /// <summary>
        /// Protocol error: server responded an unknown return code.
        /// </summary>
        ProtocolError_UnknownReturnCode,
        /// <summary>
        /// Protocol error: we connected with CleanSession=true, but the server responded the session is present.
        /// </summary>
        ProtocolError_SessionNotFlushed,
        /// <summary>
        /// Protocol error: the server sended a part of the ConnectAck then the channel was closed.
        /// </summary>
        ProtocolError_IncompleteResponse
    }
}
