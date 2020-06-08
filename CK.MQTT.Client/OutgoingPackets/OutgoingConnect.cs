using CK.MQTT.Common;
using CK.MQTT.Common.OutgoingPackets;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.OutgoingPackets
{
    class OutgoingConnect : ComplexOutgoingPacket
    {
        readonly string _protocolName;
        readonly byte _protocolLevel;
        readonly byte _flags;
        readonly string _clientId;
        readonly bool _cleanSession;
        readonly ushort _keepAlive;
        readonly string? _username;
        readonly string? _password;
        readonly string? _willTopic;
        readonly Func<int>? _getPayloadSize;
        readonly Func<PipeWriter, CancellationToken, ValueTask>? _payloadWriter;
        readonly int _sizePostPayload;

        public OutgoingConnect(
            string protocolName,
            byte protocolLevel,
            byte flags,
            string clientId,
            bool cleanSession,
            ushort keepAlive,
            string willTopic,
            Func<int>? getPayloadSize,
            Func<PipeWriter, CancellationToken, ValueTask> payloadWriter,
            string? username,
            string? password ) : this( protocolName, protocolLevel, flags, clientId, cleanSession, keepAlive, username, password )
        {
            _willTopic = willTopic;
            _getPayloadSize = getPayloadSize;
            _payloadWriter = payloadWriter;
        }

        public OutgoingConnect(
            string protocolName,
            byte protocolLevel,
            byte flags,
            string clientId,
            bool cleanSession,
            ushort keepAlive,
            string? username,
            string? password )
        {
            _protocolName = protocolName;
            _protocolLevel = protocolLevel;
            _flags = flags;
            _clientId = clientId;
            _cleanSession = cleanSession;
            _keepAlive = keepAlive;
            _username = username;
            _password = password;
            _sizePostPayload = _username?.MQTTSize() ?? 0 + _password?.MQTTSize() ?? 0;
        }

        protected override PacketType PacketType => PacketType.Connect;

        protected override byte Header => (byte)PacketType;

        protected override int RemainingSize => _sizePostPayload + _getPayloadSize?.Invoke() ?? 0 + HeaderSize;

        protected override int HeaderSize => _protocolName.MQTTSize()
                                                + 1 //_protocolLevel
                                                + 1 //_flag
                                                + 2 //_keepAlive
                                                + _clientId.MQTTSize()
                                                + _willTopic?.MQTTSize() ?? 0;

        protected override void WriteHeaderContent( Span<byte> span )
        {
            //protocol name: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349225
            span = span.WriteString( _protocolName );
            // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349228
            span[0] = _protocolLevel;
            // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349230
            span[1] = _flags;
            span = span[2..]
                //http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349238
                .WriteUInt16( _keepAlive )
                .WriteString( _clientId );
            if( _willTopic != null )
            {
                span.WriteString( _willTopic );
            }
        }

        protected override async ValueTask WriteRestOfThePacketAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            if( _payloadWriter != null )
            {
                await _payloadWriter( pw, cancellationToken );
            }
            WriteEndOfPayload( pw );
            await pw.FlushAsync( cancellationToken );
        }

        void WriteEndOfPayload( PipeWriter pw )
        {
            Span<byte> span = pw.GetSpan( _sizePostPayload );
            if( _username != null ) span = span.WriteString( _username );
            if( _password != null ) span.WriteString( _password );
            pw.Advance( _sizePostPayload );
        }
    }
}
