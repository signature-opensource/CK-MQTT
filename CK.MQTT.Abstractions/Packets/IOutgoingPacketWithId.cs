namespace CK.MQTT
{
    /// <summary>
    /// An <see cref="IOutgoingPacket"/> but with a packet id and a QoS.
    /// </summary>
    public interface IOutgoingPacketWithId : IOutgoingPacket
    {
        /// <summary>
        /// The packet id of the <see cref="IOutgoingPacket"/>.
        /// <a href="docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349268">
        /// Read the specification fore more information</a>.
        /// </summary>
        int PacketId { get; set; }

        /// <summary>
        /// The QoS of the packet. A packet with an identifier is never at QoS 0.
        /// </summary>
        QualityOfService Qos { get; }
    }
}
