using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public class SimpleMqttClient : Mqtt3ClientBase
    {
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
        }

        protected override void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData )
        {
        }

        public 
        protected override ValueTask ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken )
        {
        }
    }
}
