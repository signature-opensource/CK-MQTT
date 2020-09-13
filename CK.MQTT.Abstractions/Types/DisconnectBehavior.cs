namespace CK.MQTT
{
    public enum DisconnectBehavior
    {
        Nothing = 0,
        AutoReconnect = 1,
        CancelAcksOnDisconnect = 2,
        AutoReconnectAndCancelAcks = AutoReconnect | CancelAcksOnDisconnect
    }
}
