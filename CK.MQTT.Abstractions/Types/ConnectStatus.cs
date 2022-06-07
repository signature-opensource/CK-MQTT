namespace CK.MQTT
{
    public enum ConnectStatus
    {
        /// <summary>
        /// Connection was succesful.
        /// </summary>
        Successful = 0,
        /// <summary>
        /// Connection has been deffered to the AutoReconnect logic.
        /// This will happen only if you configured as is.
        /// </summary>
        Deffered = 1,
        /// <summary>
        /// There was an error while trying to connect, the error may be transient (ie: connectivity issue).
        /// </summary>
        ErrorMaybeRecoverable = 16,
        /// <summary>
        /// There was an unknown error.
        /// </summary>
        ErrorUnknown = 17,
        /// <summary>
        /// There was an error that is known to not be recoverable. A configuration change from the client, or server is required.
        /// </summary>
        ErrorUnrecoverable = 18
    }
}
