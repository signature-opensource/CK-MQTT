using System;

namespace CK.MQTT.Common.Packets
{
    public interface IPacket
    {
        byte HeaderByte { get; }

        uint RemainingLength { get; }

        void Serialize( Span<byte> stream );
    }
}
