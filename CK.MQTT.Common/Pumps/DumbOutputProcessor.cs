using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CK.MQTT.OutputPump;

namespace CK.MQTT
{
    public static class DumbOutputProcessor
    {
        public static async ValueTask OutputProcessor(
            IOutputLogger? m, PacketSender packetSender, Channel<IOutgoingPacket> reflexes, Channel<IOutgoingPacket> messages, CancellationToken cancellationToken, Func<DisconnectedReason, Task> _clientClose
        )
        {
            if( reflexes.Reader.TryRead( out IOutgoingPacket packet ) || messages.Reader.TryRead( out packet ) )
            {
                await packetSender( m, packet );
                return;
            }
            await Task.WhenAny( reflexes.Reader.WaitToReadAsync().AsTask(), messages.Reader.WaitToReadAsync().AsTask(), Task.Delay( -1, cancellationToken ) );
        }
    }
}
