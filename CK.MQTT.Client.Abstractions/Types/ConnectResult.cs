namespace CK.MQTT
{
    public enum ConnectError
    {
        Ok,
        ChannelNotConnected,
        ProtocolError,
        Timeout,
        OperationCanceled,
        NetworkError
    }
    public readonly struct ConnectResult
    {
        public ConnectResult( ConnectError connectError, SessionState sessionState, ConnectReturnCode connectionStatus )
        {
            ConnectError = connectError;
            SessionState = sessionState;
            ConnectionStatus = connectionStatus;
        }

        public readonly ConnectError ConnectError;

        public readonly SessionState SessionState;

        public readonly ConnectReturnCode ConnectionStatus;
    }
}
