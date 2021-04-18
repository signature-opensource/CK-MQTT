using CK.Core;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class PublishReflex : IReflexMiddleware
    {
        readonly MqttClientConfiguration _mqttConfiguration;
        readonly IIncomingPacketStore _store;
        readonly Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> _messageHandler;
        readonly OutputPump _output;

        public PublishReflex( MqttClientConfiguration mqttConfiguration, IIncomingPacketStore store, Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler, OutputPump output )
        {
            _mqttConfiguration = mqttConfiguration;
            _store = store;
            _messageHandler = messageHandler;
            _output = output;
        }
        const byte _dupFlag = 1 << 4;
        const byte _retainFlag = 1;

        public async ValueTask ProcessIncomingPacketAsync( IInputLogger? m, InputPump sender, byte header, int packetLength, PipeReader reader, Func<ValueTask> next, CancellationToken cancellationToken )
        {
            if( (PacketType)((header >> 4) << 4) != PacketType.Publish )
            {
                await next();
                return;
            }
            QualityOfService qos = (QualityOfService)((header >> 1) & 3);
            using( m?.ProcessPublishPacket( sender, header, packetLength, reader, next, qos ) )
            {
                bool dup = (header & _dupFlag) > 0;
                bool retain = (header & _retainFlag) > 0;
                if( (byte)qos > 2 ) throw new ProtocolViolationException( $"Parsed QoS byte is invalid({(byte)qos})." );
                string? topic;
                ushort packetId;
                while( true )
                {
                    ReadResult read = await reader.ReadAsync();
                    if( read.IsCanceled ) return;
                    if( qos == QualityOfService.AtMostOnce )
                    {
                        string theTopic = await reader.ReadMQTTStringAsync();
                        await _messageHandler( _mqttConfiguration.OnInputMonitor, theTopic, reader, packetLength - theTopic.MQTTSize(), qos, retain, cancellationToken );
                        return;
                    }
                    if( Publish.ParsePublishWithPacketId( read.Buffer, out topic, out packetId, out SequencePosition position ) )
                    {
                        reader.AdvanceTo( position );
                        break;
                    }
                    reader.AdvanceTo( read.Buffer.Start, read.Buffer.End );
                }
                if( qos == QualityOfService.AtLeastOnce )
                {
                    await _messageHandler( _mqttConfiguration.OnInputMonitor, topic, reader, packetLength - 2 - topic.MQTTSize(), qos, retain, cancellationToken );
                    if( !_output.QueueReflexMessage( LifecyclePacketV3.Puback( packetId ) ) ) m?.QueueFullPacketDropped( PacketType.PublishAck, packetId );
                    return;
                }
                if( qos != QualityOfService.ExactlyOnce ) throw new ProtocolViolationException();
                await _store.StoreIdAsync( m, packetId );
                await _messageHandler( _mqttConfiguration.OnInputMonitor, topic, reader, packetLength - 2 - topic.MQTTSize(), qos, retain, cancellationToken );
                if( !_output.QueueReflexMessage( LifecyclePacketV3.Pubrec( packetId ) ) ) m?.QueueFullPacketDropped( PacketType.PublishReceived, packetId );
            }
        }
    }
}
