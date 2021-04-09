using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CK.MQTT.Pumps.OutputPump;

namespace CK.MQTT
{
    public static class DumbOutputProcessor
    {
        public static async ValueTask OutputProcessor(
            IOutputLogger? m,
            PacketSender packetSender,
            Channel<IOutgoingPacket> reflexes,
            Channel<IOutgoingPacket> messages,
            Func<DisconnectedReason, Task> clientClose,
            CancellationToken cancellationToken
        )
        {
            if( reflexes.Reader.TryRead( out IOutgoingPacket packet ) || messages.Reader.TryRead( out packet ) )
            {
                await packetSender( m, packet );
                return;
            }
            await Task.WhenAny( reflexes.Reader.WaitToReadAsync( cancellationToken ).AsTask(), messages.Reader.WaitToReadAsync( cancellationToken ).AsTask() );
        }
    }
}
