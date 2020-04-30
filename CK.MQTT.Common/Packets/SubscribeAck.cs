using CK.Core;
using CK.MQTT.Common.Serialisation;
using System;
using System.Collections.Generic;

namespace CK.MQTT.Common.Packets
{
    public class SubscribeAck : IPacket
    {
        readonly uint _size;
        public SubscribeAck( ushort packetId, SubscribeReturnCode[] returnCodes )
        {
            PacketId = packetId;
            ReturnCodes = returnCodes;
            _size = (uint)(2 + returnCodes.Length);
        }

        public ushort PacketId { get; }

        public IReadOnlyCollection<SubscribeReturnCode> ReturnCodes { get; }

        public byte HeaderByte => (byte)PacketType.SubscribeAck;

        public uint RemainingLength => _size;

        public void Serialize( Span<byte> buffer )
        {
            buffer.WriteUInt16( PacketId );
            int i = 2;
            foreach( SubscribeReturnCode code in ReturnCodes )
            {
                buffer[i++] = (byte)code;
            }
        }

        public static SubscribeAck? Deserialize( IActivityMonitor m, ReadOnlySpan<byte> buffer )
        {
            if( buffer.Length < 3 )
            {
                m.Error( "Malformed Packet: Not enough bytes in the SubAck packet." );
                return null;
            }
            ushort packetId = buffer.ReadUInt16();
            buffer = buffer[2..];
            SubscribeReturnCode[] codes = new SubscribeReturnCode[buffer.Length];
            for( int i = 0; i < buffer.Length; i++ )
            {
                codes[i] = (SubscribeReturnCode)buffer[i];
            }
            return new SubscribeAck( packetId, codes );
        }
    }
}
