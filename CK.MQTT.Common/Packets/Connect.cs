using CK.Core;
using CK.MQTT.Common.Serialisation;
using System;
using System.Diagnostics;
using System.Text;

namespace CK.MQTT.Common.Packets
{
    public class Connect : IPacket
    {
        readonly ushort _size;
        public Connect(
            string clientId,
            bool cleanSession,
            ushort keepAlive,
            LastWill? will,
            string? userName,
            string? password,
            string protocolName = "MQTT" )
        {
            static ushort StrLen( string str ) => (ushort)(2 + Encoding.UTF8.GetByteCount( str ));
            // MQTT-3.1.2-22: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/csprd02/mqtt-v3.1.1-csprd02.html#_Toc385349870
            Debug.Assert( userName != null || (userName == null && password == null) );
            Debug.Assert( clientId.Length <= ushort.MaxValue );
            _size += 4;//ProtocolLevel(1)+ Flags(1) + KeepAlive(2)
            ProtocolName = protocolName;
            _size += StrLen( protocolName );
            ClientId = clientId;
            if( clientId != null ) _size += StrLen( clientId );
            CleanSession = cleanSession;
            KeepAlive = keepAlive;
            Will = will;
            if( will != null )
            {
                _size += StrLen( will.Message.Topic );
                _size += 2;
                _size += (ushort)will.Message.Payload.Length;
            }
            UserName = userName;//2+strLen
            if( userName != null ) _size += StrLen( userName );
            Password = password;
            if( password != null ) _size += StrLen( password );
            ProtocolLevel = 4;
        }

        public string ProtocolName { get; }

        public string ClientId { get; }

        public bool CleanSession { get; }

        public byte ProtocolLevel { get; }

        public ushort KeepAlive { get; }

        public LastWill? Will { get; }

        public string? UserName { get; }

        public string? Password { get; }

        const byte _usernameFlag = 0b1000_0000;
        const byte _passwordFlag = 0b0100_0000;
        const byte _willRetainFlag = 0b0010_0000;
        const byte _willFlag = 0b0000_0100;
        const byte _cleanSessionFlag = 0b0000_0010;

        byte GetFlags()
        {
            byte flags = 0;
            if( UserName != null ) flags |= _usernameFlag;
            if( Password != null ) flags |= _passwordFlag;
            if( Will != null )
            {
                if( Will.Retain ) flags |= _willRetainFlag;
                flags |= (byte)((byte)Will.QualityOfService << 3);
                flags |= _willFlag;
            }
            if( CleanSession ) flags |= _cleanSessionFlag;
            return flags;
        }
        public uint RemainingLength => _size;

        public byte ProtocoLevel { get; }

        public byte HeaderByte => (byte)PacketType.Connect;

        public void Serialize( Span<byte> buffer )
        {
            //protocol name: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349225
            buffer[0] = 0b0000_0000; //Length MSB
            buffer[1] = 0b0000_0100; //Length LSB
            buffer[2] = 0b0100_1101; //M
            buffer[3] = 0b0101_0001; //Q
            buffer[4] = 0b0101_0100; //T
            buffer[5] = 0b0101_0100; //T
            buffer[6] = ProtocolLevel;// http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349228
            buffer[7] = GetFlags(); // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349230
            MqttBinaryWriter.WriteUInt16( buffer[8..], KeepAlive ); //http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349238
            buffer = buffer[10..];
            buffer = buffer.WriteString( ClientId );
            if( Will != null )
            {
                buffer = buffer
                    .WriteString( Will.Message.Topic )
                    .WritePayload( Will.Message.Payload );
            }
            if( UserName != null ) buffer = buffer.WriteString( UserName );
            if( Password != null ) buffer = buffer.WriteString( Password );
            Debug.Assert( buffer.Length == 0 );
        }

        const byte _shiftedPacketId = (byte)PacketType.Connect >> 4;

        public static Connect? Deserialize( IActivityMonitor m, ReadOnlyMemory<byte> memory, bool allowInvalidMagic )
        {
            const string notEnoughBytes = "Malformed packet: Not enough bytes in the Connect packet.";
            ReadOnlySpan<byte> buffer;
            string? protocolName;
            if( memory.Length < 8 )
            {
                //Even by changing the magic string to an empty string, we should get 8 bytes.
                m.Error( notEnoughBytes + "0" );
                return null;
            }
            if( !allowInvalidMagic )
            {
                buffer = memory.Span;
                if(
                    buffer[0] != 0b0000_0000 || //Length MSB
                    buffer[1] != 0b0000_0100 || //Length LSB
                    buffer[2] != 0b0100_1101 || //M
                    buffer[3] != 0b0101_0001 || //Q
                    buffer[4] != 0b0101_0100 || //T
                    buffer[5] != 0b0101_0100    //T
                )
                {
                    m.Error( "Malformed packet: Invalid magic string." );
                    return null;
                }
                protocolName = "MQTT";
                memory = memory[6..];
            }
            else
            {
                memory = memory.ReadString( out protocolName );
                if( protocolName == null )
                {
                    m.Error( notEnoughBytes + "1" );
                    return null;
                }
            }
            buffer = memory.Span;
            if( buffer.Length < 6 )//We must get at least 6 bytes here. 
            {
                m.Error( notEnoughBytes + "2" );
                return null;
            }
            byte protocolLevel = buffer[0];
            if( protocolLevel != 4 )
            {
                m.Error( $"Unsupported protocol level: '{protocolLevel}'." );
                return null;
            }
            byte flags = buffer[1];
            buffer[2..].ReadUInt16( out ushort keepAlive );
            memory = memory[2..].ReadString( out string? clientId );
            if( clientId == null )
            {
                m.Error( notEnoughBytes + "3" );
                return null;
            }
            LastWill? will = null;
            if( (flags & _willFlag) > 0 )
            {
                if( memory.Length < 4 ) //If will flag is present, there should be at least 4 bytes available.
                {
                    m.Error( notEnoughBytes + "4" );
                    return null;
                }
                var qos = (QualityOfService)((flags & ((byte)QualityOfService.Mask << 3)) >> 3);
                bool retain = (flags & _willRetainFlag) > 0;
                memory = memory.ReadString( out string? topic )
                    .ReadPayload( out ReadOnlyMemory<byte>? payload );
                if( topic == null || !payload.HasValue )
                {
                    m.Error( notEnoughBytes + "5" );
                    return null;
                }
                will = new LastWill( new ApplicationMessage( topic, payload.Value ), qos, retain );
            }
            bool usernamePresent = (flags & _usernameFlag) > 0;
            bool passwordPresent = (flags & _passwordFlag) > 0;
            memory = memory.ConditionallyReadString( out string? username, usernamePresent );
            if( usernamePresent && username == null )
            {
                m.Error( notEnoughBytes + "6" );
                return null;
            }
            memory = memory.ConditionallyReadString( out string? password, passwordPresent );
            if( passwordPresent && password == null )
            {
                m.Error( notEnoughBytes + "7" );
                return null;
            }
            if( memory.Length != 0 )
            {
                m.Warn( $"Malformed Packet: There is {memory.Length} unparsed bytes in the Connect packet !" );
            }
            bool clean = (flags & _cleanSessionFlag) > 0;
            return new Connect( clientId, clean, keepAlive, will, username, password, protocolName );
        }
    }
}
