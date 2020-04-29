using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Common.Packets
{
    public interface IPacketWithId : IPacket
    {
        public ushort PacketId { get; }
    }
}
