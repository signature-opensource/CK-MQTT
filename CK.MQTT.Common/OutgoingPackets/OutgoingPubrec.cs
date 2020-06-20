using CK.MQTT.Common.Packets;

namespace CK.MQTT.Common.OutgoingPackets
{
    public class OutgoingPubrec : OutgoingPacketWithId
    {
        public OutgoingPubrec( ushort packetId ) : base( packetId )
        {
        }

        public override byte Header => (byte)PacketType.PublishReceived;
    }
}
