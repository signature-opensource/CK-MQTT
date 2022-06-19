namespace CK.MQTT
{
    public enum ManualConnectBehavior
    {
        TryOnce,
        RetryUntilConnectedOrUnrecoverable,
        RetryUntilConnected,
        TryOnceThenRetryInBackground,
        UseSinkBehavior
    }
}
