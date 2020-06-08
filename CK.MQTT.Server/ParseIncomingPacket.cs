//using CK.Core;
//using CK.MQTT.Common.Serialisation;
//using CK.MQTT.Common.Stores;
//using System;
//using System.Buffers;
//using System.ComponentModel;
//using System.Diagnostics;
//using System.IO.Pipelines;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace CK.MQTT.Common.Packets
//{
//    public abstract class ParseIncomingPacket : IPacket
//    {
//        //readonly ushort _size;
//        protected ParseIncomingPacket(
//            string clientId,
//            bool cleanSession,
//            ushort keepAlive,
//            string? userName,
//            string? password,
//            string protocolName = "MQTT" )
//        {
//            static ushort StrLen( string str ) => (ushort)(2 + Encoding.UTF8.GetByteCount( str ));
//            // MQTT-3.1.2-22: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/csprd02/mqtt-v3.1.1-csprd02.html#_Toc385349870
//            Debug.Assert( userName != null || (userName == null && password == null) );
//            Debug.Assert( clientId.Length <= ushort.MaxValue );
//            //_size += 4;//ProtocolLevel(1)+ Flags(1) + KeepAlive(2)
//            ProtocolName = protocolName;
//            //_size += StrLen( protocolName );
//            ClientId = clientId;
//            //if( clientId != null ) _size += StrLen( clientId );
//            CleanSession = cleanSession;
//            KeepAlive = keepAlive;
//            //Will = will;
//            //if( will != null )
//            {
//                //_size += StrLen( will.Message.Topic );
//                //_size += 2;
//                //_size += (ushort)will.Message.Payload.Length;
//            }
//            UserName = userName;//2+strLen
//            //if( userName != null ) _size += StrLen( userName );
//            Password = password;
//            //if( password != null ) _size += StrLen( password );
//            ProtocolLevel = 4;
//        }

//        public string ProtocolName { get; }

//        public string ClientId { get; }

//        public bool CleanSession { get; }

//        public byte ProtocolLevel { get; }

//        public ushort KeepAlive { get; }

//        public LastWill? Will { get; }

//        public string? UserName { get; }

//        public string? Password { get; }

//        const byte _usernameFlag = 0b1000_0000;
//        const byte _passwordFlag = 0b0100_0000;
//        const byte _willRetainFlag = 0b0010_0000;
//        const byte _willFlag = 0b0000_0100;
//        const byte _cleanSessionFlag = 0b0000_0010;

//        byte GetFlags()
//        {
//            byte flags = 0;
//            if( UserName != null ) flags |= _usernameFlag;
//            if( Password != null ) flags |= _passwordFlag;
//            if( Will != null )
//            {
//                if( Will.Retain ) flags |= _willRetainFlag;
//                flags |= (byte)((byte)Will.QualityOfService << 3);
//                flags |= _willFlag;
//            }
//            if( CleanSession ) flags |= _cleanSessionFlag;
//            return flags;
//        }
//        public uint RemainingLength => _size;

//        public byte ProtocoLevel { get; }

//        public byte HeaderByte => (byte)PacketType.Connect;

//        public void Serialize( PipeWriter writer )
//        {
//            Span<byte> buffer = writer.GetSpan( 11 );
//            //protocol name: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349225
//            buffer[0] = 0b0000_0000; //Length MSB
//            buffer[1] = 0b0000_0100; //Length LSB
//            buffer[2] = 0b0100_1101; //M
//            buffer[3] = 0b0101_0001; //Q
//            buffer[4] = 0b0101_0100; //T
//            buffer[5] = 0b0101_0100; //T
//            buffer[6] = ProtocolLevel;// http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349228
//            buffer[7] = GetFlags(); // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349230
//            MqttBinaryWriter.WriteUInt16( buffer[8..], KeepAlive ); //http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349238
//            buffer = buffer[10..];
//            buffer = buffer.WriteString( ClientId );
//            if( Will != null )
//            {
//                buffer = buffer
//                    .WriteString( Will.Message.Topic )
//                    .WritePayload( Will.Message.Payload );
//            }
//            if( UserName != null ) buffer = buffer.WriteString( UserName );
//            if( Password != null ) buffer = buffer.WriteString( Password );
//            Debug.Assert( buffer.Length == 0 );
//        }

//        const byte _shiftedPacketId = (byte)PacketType.Connect >> 4;

//        static bool DeserializeHeader( ReadOnlySequence<byte> buffer,
//            out string? protocolName,
//            out byte protocolLevel,
//            out byte flags,
//            out ushort keepAlive,
//            out string? clientId,
//            out string? willTopic )
//        {
//            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
//            bool a = reader.TryReadMQTTString( out protocolName );
//            bool b = reader.TryRead( out protocolLevel );
//            bool c = reader.TryRead( out flags );
//            bool d = reader.TryReadBigEndian( out keepAlive );
//            bool e = reader.TryReadMQTTString( out clientId );
//            bool f;
//            if( (_willFlag & flags) > 0 )
//            {
//                f = reader.TryReadMQTTString( out willTopic );
//            }
//            else
//            {
//                f = true;
//                willTopic = null;
//            }
//            return a && b && c && d && e && f;
//        }

//        public static async ValueTask<bool> DeserializeAsync( IActivityMonitor m, PipeReader pipe, bool allowInvalidMagic, IPacketStoreManager packetStoreManager, CancellationToken cancellationToken )
//        {
//            ReadResult result = await pipe.ReadAsync( cancellationToken );
//            if( cancellationToken.IsCancellationRequested ) return false;
//            bool NotEnoughByte()
//            {
//                m.Error( "Malformed packet: Not enough bytes in the Connect packet." );
//                return false;
//            }
//            string? protocolName;
//            byte protocolLevel;
//            byte flags;
//            ushort keepAlive;
//            string? clientId;
//            string? willTopic;
//            do
//            {
//                if( result.IsCanceled || result.IsCompleted ) return NotEnoughByte();
//                result = await pipe.ReadAsync( cancellationToken );
//                if( cancellationToken.IsCancellationRequested ) return NotEnoughByte();
//            } while( !DeserializeHeader( result.Buffer,
//                out protocolName,
//                out protocolLevel,
//                out flags,
//                out keepAlive,
//                out clientId,
//                out willTopic ) );

//        }
//    }
//}
