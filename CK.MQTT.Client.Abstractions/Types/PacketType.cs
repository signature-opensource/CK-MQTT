namespace CK.MQTT
{
    /// <summary>
    /// Represents one of the possible MQTT packet types.
    /// http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc374395205
    /// </summary>
	public enum PacketType : byte
    {
        /// <summary>
        /// MQTT CONNECT packet
        /// </summary>
        Connect = 0x10,
        /// <summary>
        /// MQTT CONNACK packet
        /// </summary>
        ConnectAck = 0x20,
        /// <summary>
        /// MQTT PUBLISH packet
        /// </summary>
        Publish = 0x30,
        /// <summary>
        /// MQTT PUBACK packet
        /// </summary>
        PublishAck = 0x40,
        /// <summary>
        /// MQTT PUBREC packet
        /// </summary>
        PublishReceived = 0x50,
        /// <summary>
        /// MQTT PUBREL packet
        /// </summary>
        PublishRelease = 0x60,
        /// <summary>
        /// MQTT PUBCOMP packet
        /// </summary>
        PublishComplete = 0x70,
        /// <summary>
        /// MQTT SUBSCRIBE packet
        /// </summary>
        Subscribe = 0x80,
        /// <summary>
        /// MQTT SUBACK packet
        /// </summary>
        SubscribeAck = 0x90,
        /// <summary>
        /// MQTT UNSUBSCRIBE packet
        /// </summary>
        Unsubscribe = 0xA0,
        /// <summary>
        /// MQTT UNSUBACK packet
        /// </summary>
        UnsubscribeAck = 0xB0,
        /// <summary>
        /// MQTT PINGREQ packet
        /// </summary>
        PingRequest = 0xC0,
        /// <summary>
        /// MQTT PINGRESP packet
        /// </summary>
        PingResponse = 0xD0,
        /// <summary>
        /// MQTT DISCONNECT packet
        /// </summary>
        Disconnect = 0xE0
    }
}
