using CK.Core;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Stores;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Processes
{
    static class UnsubscribeProcess
    {
        public static async Task<Task<bool>> ExecuteUnsubscribeProtocol( IActivityMonitor m, IMqttChannel<IPacket> channel, IPacketStore store, IEnumerable<string> topics, int waitTimeoutMs )
        {
            ushort id = await store.GetNewPacketId( m );
            Unsubscribe unsub = new Unsubscribe( id, topics );
            Task<UnsubscribeAck?> task = await channel.SendAndWaitResponse<IPacket, UnsubscribeAck>(
                m, unsub, ( p ) => p.PacketId == id, waitTimeoutMs
            );
            return TaskContinuation( m, store, id, task );
        }

        static async Task<bool> TaskContinuation( IActivityMonitor m, IPacketStore store, ushort packetId, Task<UnsubscribeAck?> task )
        {
            UnsubscribeAck? result = await task;
            if( result == null ) return false;
            await store!.FreePacketIdAsync( m, result.PacketId );
            return true;
        }
    }
}
