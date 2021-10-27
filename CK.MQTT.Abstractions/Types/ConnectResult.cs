using System;

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
        Ok = 0,
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
        /// <summary>
        /// Other reasons...
        /// </summary>
        Other = byte.MaxValue
    }

    /// <summary>
    /// Represent the result of a connect operation.
    /// </summary>
    public readonly struct ConnectResult // TODO net6: Use struct records.
    {
        /// <summary>
        /// The error if the client could not connect.
        /// </summary>
        public readonly ConnectError ConnectError;

        /// <summary>
        /// The state of the sessions.
        /// </summary>
        public readonly SessionState SessionState;

        /// <summary>
        /// The connect return code.
        /// </summary>
        public readonly ConnectReturnCode ConnectReturnCode;

        /// <summary>
        /// Instantiate a new <see cref="ConnectResult"/> where the result is an error.
        /// <see cref="SessionState"/> will be <see cref="SessionState.Unknown"/>.
        /// <see cref="ConnectReturnCode"/> will be <see cref="ConnectReturnCode.Unknown"/>.
        /// </summary>
        /// <param name="connectError">The reason the client could not connect.</param>
        public ConnectResult( ConnectError connectError )
        {
            ConnectError = connectError;
            SessionState = SessionState.Unknown;
            ConnectReturnCode = ConnectReturnCode.Unknown;
        }

        /// <summary>
        /// Instantiate a new <see cref="ConnectResult"/> where the client got connected.
        /// <see cref="ConnectError"/> will be <see cref="ConnectError.Ok"/>.
        /// </summary>
        /// <param name="sessionState">The session state.</param>
        /// <param name="connectReturnCode">The connection return code.</param>
        public ConnectResult( SessionState sessionState, ConnectReturnCode connectReturnCode )
        {
            ConnectError = connectReturnCode == ConnectReturnCode.Accepted ? ConnectError.Ok : ConnectError.SeeReturnCode;
            SessionState = sessionState;
            ConnectReturnCode = connectReturnCode;
        }

        public override bool Equals( object obj )
            => obj is ConnectResult result
            && result.ConnectError == ConnectError
            && result.ConnectReturnCode == ConnectReturnCode
            && result.SessionState == SessionState;

        public override int GetHashCode() => HashCode.Combine( SessionState, ConnectError, ConnectReturnCode );

        public static bool operator ==( ConnectResult left, ConnectResult right ) => left.Equals( right );

        public static bool operator !=( ConnectResult left, ConnectResult right ) => !(left == right);
    }
}
