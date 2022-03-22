using CK.Core;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{

    internal static class TestMqttClient
    {
        public static MqttClientFactory Factory { get; } = new();
        public class MqttClientFactory
        {

            public SimpleTestMqtt3Client CreateMQTT3Client(
                Mqtt3ClientConfiguration config,
                Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> messageHandler )
            {
                return new SimpleTestMqtt3Client( config, messageHandler );
            }

            public SimpleTestMqtt3Client CreateMQTT3Client(
                Mqtt3ClientConfiguration config,
                Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> messageHandler )
            {
                return new SimpleTestMqtt3Client( config, messageHandler );
            }
        }
    }
    public class SimpleTestMqtt3Client : MQTT3ClientBase
    {
        readonly Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask>? _disposableMessageHandler;
        readonly Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> _newMessageHandler;
        public SimpleTestMqtt3Client( Mqtt3ClientConfiguration config, Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> messageHandler )
            : base( config )
        {
            _disposableMessageHandler = messageHandler;
        }
        public SimpleTestMqtt3Client( Mqtt3ClientConfiguration config, Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> messageHandler )
            : base( config )
        {
            _newMessageHandler = messageHandler;
        }


        protected override void OnPacketResent( ushort packetId, int resentCount, bool isDropped )
        {
        }

        protected override void OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount )
        {
        }

        protected override void OnReconnect()
        {
        }

        protected override void OnStoreFull( ushort freeLeftSlot )
        {
        }

        protected override void OnUnattendedDisconnect( DisconnectReason reason )
        {
            DisconnectedHandler?.Invoke( reason );
        }

        public int OnUnparsedExtraDataCount { get; private set; }
        protected override void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData )
        {
            OnUnparsedExtraDataCount++;
        }

        /// <summary>
        /// <see langword="delegate"/> used when the client is Disconnected.
        /// </summary>
        /// <param name="reason">The reason of the disconnection.</param>
        /// <param name="selfReconnectTask">Not null when <see cref="ProtocolConfiguration"/>. after the connection has been lost </param>
        public delegate void Disconnected( DisconnectReason reason );

        public Disconnected? DisconnectedHandler { get; set; }

        protected override async ValueTask ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken )
        {
            if( _disposableMessageHandler  != null)
            {
                await new DisposableMessageClosure( _disposableMessageHandler ).HandleMessageAsync( topic, reader, size, q, retain, cancellationToken );
            }
            if(_newMessageHandler != null)
            {
                await new NewApplicationMessageClosure(_newMessageHandler).HandleMessageAsync(topic, reader, size, q, retain, cancellationToken );
            }
        }
    }
}
