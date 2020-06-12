//using CK.Core;
//using CK.MQTT.Common.Serialisation;
//using System;
//using System.Buffers;
//using System.Collections.Generic;
//using System.Diagnostics;

//namespace CK.MQTT.Common.Packets
//{
//    public class SubscribeAck : IPacket
//    {
//        readonly uint _size;
//        public SubscribeAck( ushort packetId, SubscribeReturnCode[] returnCodes )
//        {
//            PacketId = packetId;
//            ReturnCodes = returnCodes;
//            _size = (uint)(2 + returnCodes.Length);
//        }

//        public ushort PacketId { get; }

//        public IReadOnlyCollection<SubscribeReturnCode> ReturnCodes { get; }

//        public byte HeaderByte => (byte)PacketType.SubscribeAck;

//        public uint RemainingLength => _size;

//        public void Serialize( Span<byte> buffer )
//        {
//            buffer.WriteUInt16( PacketId );
//            int i = 2;
//            foreach( SubscribeReturnCode code in ReturnCodes )
//            {
//                buffer[i++] = (byte)code;
//            }
//        }

//        public static SubscribeAck? Deserialize( IActivityMonitor m, ReadOnlySequence<byte> buffer )
//        {
//            var reader = new SequenceReader<byte>( buffer );
//            if( reader.TryReadBigEndian( out ushort packetId ) )
//            {
//                m.Error( "Malformed Packet: Not enough bytes in the SubAck packet." );
//                return null;
//            }
//            long remaining = reader.Remaining;
//            SubscribeReturnCode[] codes = new SubscribeReturnCode[remaining];
//            for( int i = 0; i < remaining; i++ )
//            {
//                _ = reader.TryRead( out byte code );
//                codes[i] = (SubscribeReturnCode)code;
//            }
//            Debug.Assert( reader.Remaining == 0 );
//            return new SubscribeAck( packetId, codes );
//        }
//    }
//}
