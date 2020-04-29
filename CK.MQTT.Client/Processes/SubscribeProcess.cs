using CK.Core;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Stores;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Processes
{
    public static class SubscribeProcess
    {
        public static async Task<Task<IReadOnlyCollection<SubscribeReturnCode>?>> ExecuteSubscribeProtocol( IActivityMonitor m,
            IMqttChannel<IPacket> channel, IPacketStore store, Subscription[] subscriptions, int timeoutMs )
        {
            ushort id = await store.GetNewPacketId( m );
            var subscribe = new Subscribe( id, subscriptions );
            Task<SubscribeAck?> task = await channel.SendAndWaitResponseAndLog<IPacket, SubscribeAck>(
                m, subscribe, ( p ) => p.PacketId == id, timeoutMs
            );
            return TaskContinuation( m, store, id, task );
        }


        static async Task<IReadOnlyCollection<SubscribeReturnCode>?> TaskContinuation( IActivityMonitor m, IPacketStore store, ushort packetId, Task<SubscribeAck?> task )
        {
            SubscribeAck? result = await task;
            if( result == null ) return null;
            await store.FreePacketIdAsync( m, result.PacketId );
            return result.ReturnCodes;
        }
    }
}
