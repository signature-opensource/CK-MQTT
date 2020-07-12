namespace CK.MQTT
{
    /// <summary>
    /// Maximum QoS level that was granted for a Subscription.
    /// <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc384800444">See specification for more information</a>.
    /// </summary>
    public enum SubscribeReturnCode : byte
    {
        /// <summary>
        /// Success - Maximum QoS 0.
        /// </summary>
        MaximumQoS0 = 0x00,
        /// <summary>
        /// Success - Maximum QoS 1.
        /// </summary>
        MaximumQoS1 = 0x01,
        /// <summary>
        /// Success - Maximum QoS 2.
        /// </summary>
        MaximumQoS2 = 0x02,
        /// <summary>
        /// Failure.
        /// </summary>
        Failure = 0x80
    }
}
