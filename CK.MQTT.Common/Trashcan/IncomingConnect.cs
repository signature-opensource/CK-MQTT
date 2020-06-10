//using CK.Core;
//using System;
//using System.Buffers;
//using System.Collections.Generic;
//using System.IO.Pipelines;
//using System.Text;
//using System.Threading.Tasks;

//namespace CK.MQTT.Common.Packets
//{
//    class IncomingConnect : Connect
//    {
//        IncomingConnect(
//            string clientId,
//            bool cleanSession,
//            ushort keepAlive,
//            IncomingLastWill lastWill,
//            string? userName,
//            string? password,
//            string protocolName = "MQTT")
//            : base( clientId, cleanSession, keepAlive, userName, password, protocolName )
//        {
//            LastWill = lastWill;
//        }

//        public IncomingLastWill LastWill { get; }

//        public ValueTask<IncomingConnect?> Deserialize(
//            IActivityMonitor m, ReadOnlySequence<byte> sequence, bool allowInvalidMagic)
//        {
//            const string notEnoughBytes = "Malformed packet: Not enough bytes in the Connect packet.";
//            SequenceReader<byte> reader = new SequenceReader<byte>();
//            if( !reader.TryReadMQTTString( out string? protocolName )
//             || (!allowInvalidMagic && protocolName != "MQTT")
//             || !reader.TryRead( out byte protocolLevel )
//             || !reader.TryRead( out byte flags )
//             || !reader.TryReadBigEndian( out ushort keepAlive )
//             || !reader.TryReadMQTTString( out string? clientId )
//             )
//            {
//                m.Error( notEnoughBytes );
//                return null;
//            }
//            if( protocolLevel != 4 )
//            {
//                m.Error( $"Unsupported protocol level: '{protocolLevel}'." );
//                return null;
//            }
//            LastWill? will = null;
//            if( (flags & _willFlag) > 0 )
//            {
//                var qos = (QualityOfService)((flags & ((byte)QualityOfService.Mask << 3)) >> 3);
//                bool retain = (flags & _willRetainFlag) > 0;
//                if( !reader.TryReadMQTTString( out string? topic )
//                    || !reader.TryReadMQTTPayload( out ReadOnlySequence<byte> payload )
//                    )
//                {
//                    m.Error( notEnoughBytes );
//                    return null;
//                }
//                will = new LastWill( new OutgoingApplicationMessage( topic, payload ), qos, retain );
//            }
//            string? username = null;
//            string? password = null;
//            if( ((flags & _usernameFlag) > 0 && !reader.TryReadMQTTString( out username ))
//             || ((flags & _passwordFlag) > 0 && !reader.TryReadMQTTString( out password )) )
//            {
//                m.Error( notEnoughBytes );
//                return null;
//            }
//            if( reader.Remaining > 0 )
//            {
//                m.Warn( $"Malformed Packet: There is {sequence.Length} unparsed bytes in the Connect packet !" );
//            }
//            bool clean = (flags & _cleanSessionFlag) > 0;
//            return new Connect( clientId, clean, keepAlive, will, username, password, protocolName );
//        }
//    }
//}
