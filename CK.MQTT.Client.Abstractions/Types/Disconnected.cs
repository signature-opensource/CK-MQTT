namespace CK.MQTT
{
    /// <summary>
    /// <see langword="delegate"/> used when the client is Disconnected.
    /// </summary>
    /// <param name="reason">The reason of the disconnection.</param>
    public delegate void Disconnected( DisconnectedReason reason );
}
