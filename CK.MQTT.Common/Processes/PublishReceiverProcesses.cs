using CK.Core;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Stores;
using System;
using System.Threading.Tasks;
namespace CK.MQTT.Common.Processes
{
    public static class PublishReceiverProcesses
    {
        public static async Task OnPublishQoS1(
            IActivityMonitor m,
            IMqttChannel<IPacket> channel, Func<Publish, Task> messageDeliverer,
            PublishWithId publish
            )
        {
            await messageDeliverer( publish );
            var pubAck = new PublishAck( publish.PacketId );
            await channel.SendAsync( m, pubAck, default );
        }

        public static async Task OnPublishQoS2( IActivityMonitor m,
            IMqttChannel<IPacket> channel, IPacketStore messageStore, Func<PublishWithId, Task> messageDeliverer,
            PublishWithId publish
            )
        {
            bool firstTime = await messageStore.StorePacketIdAsync( m, publish.PacketId );
            PublishReceived pubRec = new PublishReceived( publish.PacketId );
            if( firstTime )
            {
                await messageDeliverer( publish );
            }
            await channel.SendAsync( m, pubRec, default );
        }

        public static async Task OnPublishRelease( IActivityMonitor m,
            IMqttChannel<IPacket> channel, IPacketStore messageStore,
            PublishRelease pubRel )
        {
            await messageStore.FreePacketIdAsync( m, pubRel.PacketId );
            var pubComp = new PublishComplete( pubRel.PacketId );
            await channel.SendAsync( m, pubComp, default );
        }

    }
}
