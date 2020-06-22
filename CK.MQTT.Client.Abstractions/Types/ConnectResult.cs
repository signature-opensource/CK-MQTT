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
        public ConnectResult( ConnectError connectError )
        {
            ConnectError = connectError;
            SessionState = SessionState.Unknown;
            ConnectionStatus = ConnectReturnCode.Unknown;
        }

        public ConnectResult( SessionState sessionState, ConnectReturnCode connectionStatus )
        {
            ConnectError = ConnectError.Ok;
            SessionState = sessionState;
            ConnectionStatus = connectionStatus;
        }

        public readonly ConnectError ConnectError;

        public readonly SessionState SessionState;

        public readonly ConnectReturnCode ConnectionStatus;
    }
}
