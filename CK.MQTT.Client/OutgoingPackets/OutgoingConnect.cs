using System;
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
            OutgoingLastWill? lastWill = null )
        {
            _pConf = pConf;
            _mConf = mConf;
            _creds = creds;
            _lastWill = lastWill;
            _flags = ByteFlag( creds, lastWill );
            _sizePostPayload = creds?.UserName?.MQTTSize() ?? 0 + creds?.Password?.MQTTSize() ?? 0;
        }

        protected override int PayloadSize => _sizePostPayload + _lastWill?.Size ?? 0;

        protected override byte Header => (byte)PacketType.Connect;

        protected override int HeaderSize => _pConf.ProtocolName.MQTTSize()
                                                + 1 //_protocolLevel
                                                + 1 //_flag
                                                + 2 //_keepAlive
                                                + _creds?.ClientId.MQTTSize() ?? 2;

        protected override void WriteHeaderContent( Span<byte> span )
        {
            //protocol name: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349225
            span = span.WriteMQTTString( _pConf.ProtocolName );
            // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349228
            span[0] = _pConf.ProtocolLevel;
            // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349230
            span[1] = _flags;
            span = span[2..]
                //http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349238
                .WriteUInt16( _mConf.KeepAliveSecs )
                .WriteMQTTString( _creds?.ClientId ?? "" );
        }

        protected override async ValueTask<WriteResult> WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            if( _lastWill != null )
            {
                WriteResult res = await _lastWill.WriteAsync( pw, cancellationToken );
                if( res != WriteResult.Written ) return res;
            }
            WriteEndOfPayload( pw );
            await pw.FlushAsync( cancellationToken );
            return WriteResult.Written;
        }

        void WriteEndOfPayload( PipeWriter pw )
        {
            Span<byte> span = pw.GetSpan( _sizePostPayload );
            string? username = _creds?.UserName;
            string? password = _creds?.Password;
            if( username != null ) span = span.WriteMQTTString( username );
            if( password != null ) span.WriteMQTTString( password );
            pw.Advance( _sizePostPayload );
        }
    }
}