using CK.Core;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Packets
{
    public interface IPacket
    {
        byte HeaderByte { get; }

        uint RemainingLength { get; }

        void Serialize( Span<byte> stream );
    }
}
