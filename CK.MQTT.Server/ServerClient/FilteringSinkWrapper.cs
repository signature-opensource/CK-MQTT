using CK.MQTT.Client;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server.ServerClient
{
    class FilteringSinkWrapper : IMqtt3Sink
    {
        readonly IMqtt3Sink _sink;
        readonly ITopicFilter _topicFilter;
        public FilteringSinkWrapper( IMqtt3Sink sink, ITopicFilter topicFilter )
        {
            _sink = sink;
            _topicFilter = topicFilter;
        }

        public void Connected() => _sink.Connected();

        public IMqtt3Sink.ManualConnectRetryBehavior OnFailedManualConnect( ConnectResult connectResult )
            => _sink.OnFailedManualConnect( connectResult );

        public void OnPacketResent( ushort packetId, int packetInTransitOrLost, bool isDropped )
            => _sink.OnPacketResent( packetId, packetInTransitOrLost, isDropped );

        public void OnPacketWithDupFlagReceived( PacketType packetType ) => _sink.OnPacketWithDupFlagReceived( packetType );

        public void OnQueueFullPacketDropped( ushort packetId, PacketType packetType )
            => _sink.OnQueueFullPacketDropped( packetId, packetType );

        public void OnQueueFullPacketDropped( ushort packetId )
        {
            _sink.OnQueueFullPacketDropped( packetId );
        }

        public void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData )
            => _sink.OnUnparsedExtraData( packetId, unparsedData );

        public async ValueTask ReceiveAsync( string topic,
                                            PipeReader reader,
                                            uint size,
                                            QualityOfService q,
                                            bool retain,
                                            CancellationToken cancellationToken )
        {
            if( _topicFilter.IsFiltered( topic ) )
            {
                await reader.SkipBytesAsync( _sink, 0, size, cancellationToken );
                return;
            }
            await _sink.ReceiveAsync( topic, reader, size, q, retain, cancellationToken );
        }

        public bool OnUnattendedDisconnect( DisconnectReason reason ) => _sink.OnUnattendedDisconnect( reason );

        public ValueTask<bool> OnReconnectionFailedAsync( ConnectResult result ) => throw new NotSupportedException();
    }
}
