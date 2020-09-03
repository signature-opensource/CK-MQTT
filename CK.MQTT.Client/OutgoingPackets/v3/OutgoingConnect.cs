using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static CK.MQTT.IOutgoingPacket;

namespace CK.MQTT
{
    class OutgoingConnect : ComplexOutgoingPacket
    {
        readonly MqttConfiguration _mConf;
        readonly MqttClientCredentials? _creds;
        private readonly OutgoingLastWill? _lastWill;
        readonly ProtocolConfiguration _pConf;
        readonly byte _flags;
        readonly int _sizePostPayload;

        const byte _usernameFlag = 0b1000_0000;
        const byte _passwordFlag = 0b0100_0000;
        const byte _willRetainFlag = 0b0010_0000;
        const byte _willFlag = 0b0000_0100;
        const byte _cleanSessionFlag = 0b0000_0010;
        static byte ByteFlag( MqttClientCredentials? creds, OutgoingLastWill? lastWill )
        {
            byte flags = 0;
            if( creds?.UserName != null ) flags |= _usernameFlag;
            if( creds?.Password != null ) flags |= _passwordFlag;
            if( lastWill?.Retain ?? false ) flags |= _willRetainFlag;
            flags |= (byte)((byte)(lastWill?.Qos ?? 0) << 3);
            if( lastWill != null ) flags |= _willFlag;
            if( creds?.CleanSession ?? true ) flags |= _cleanSessionFlag;
            return flags;
        }

        public OutgoingConnect(
            ProtocolConfiguration pConf,
            MqttConfiguration mConf,
            MqttClientCredentials? creds,
            OutgoingLastWill? lastWill = null,
            uint sessionExpiryInterval = 0,
            ushort receiveMaximum = ushort.MaxValue,
            int maximumPacketSize = 268435455,
            ushort topicAliasMaximum = 0,
            IReadOnlyList<(string, string)>? userProperties = null
            )
        {
            _pConf = pConf;
            _mConf = mConf;
            _creds = creds;
            _lastWill = lastWill;
            _flags = ByteFlag( creds, lastWill );
            _sizePostPayload = creds?.UserName?.MQTTSize() ?? 0 + creds?.Password?.MQTTSize() ?? 0;
        }

        protected override int GetPayloadSize( ProtocolLevel protocolLevel )
            => _sizePostPayload + _lastWill?.GetSize( protocolLevel ) ?? 0;

        protected override byte Header => (byte)PacketType.Connect;

        protected override int GetHeaderSize( ProtocolLevel protocolLevel )
        {
            return _pConf.ProtocolName.MQTTSize()
                + 1 //_protocolLevel
                + 1 //_flag
                + 2 //_keepAlive
                + _creds?.ClientId.MQTTSize() ?? 2;
        }

        protected override void WriteHeaderContent( ProtocolLevel protocolLevel, Span<byte> span )
        {
            Debug.Assert( protocolLevel == _pConf.ProtocolLevel );

            span = span.WriteMQTTString( _pConf.ProtocolName ); //protocol name: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349225
            // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349228
            span[0] = (byte)_pConf.ProtocolLevel; //http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349230

            span[1] = _flags;
            span = span[2..].WriteUInt16( _mConf.KeepAliveSecs )//http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349238
                .WriteMQTTString( _creds?.ClientId ?? "" );
            if(protocolLevel == ProtocolLevel.MQTT5)
            {

            }
            Debug.Assert( span.Length == 0 );
        }

        protected override async ValueTask<WriteResult> WritePayloadAsync( ProtocolLevel protocolLevel, PipeWriter pw, CancellationToken cancellationToken )
        {
            if( _lastWill != null )
            {
                WriteResult res = await _lastWill.WriteAsync( protocolLevel, pw, cancellationToken );
                if( res != WriteResult.Written ) return res;
            }
            WriteEndOfPayload( pw );
            await pw.FlushAsync( cancellationToken );
            return WriteResult.Written;
        }

        void WriteEndOfPayload( PipeWriter pw )
        {
            if( _sizePostPayload == 0 ) return;
            Span<byte> span = pw.GetSpan( _sizePostPayload );
            string? username = _creds?.UserName;
            string? password = _creds?.Password;
            if( username != null ) span = span.WriteMQTTString( username );
            if( password != null ) span.WriteMQTTString( password );
            pw.Advance( _sizePostPayload );
        }
    }
}
