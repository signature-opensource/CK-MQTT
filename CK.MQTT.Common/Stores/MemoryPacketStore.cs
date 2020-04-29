using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
    public class MemoryPacketStore : IPacketStore
    {
        public IEnumerable<StoredApplicationMessage> AllStoredMessages => throw new NotImplementedException();

        public IEnumerable<ushort> OrphansPacketsId => throw new NotImplementedException();

        public Task CloseAsync( IActivityMonitor m )
        {
            throw new NotImplementedException();
        }

        public ValueTask<QualityOfService> DiscardMessageFromIdAsync( IActivityMonitor m, ushort packetId )
        {
            throw new NotImplementedException();
        }

        public ValueTask<bool> FreePacketIdAsync( IActivityMonitor m, ushort packetId )
        {
            throw new NotImplementedException();
        }

        public ValueTask<ushort> GetNewPacketId( IActivityMonitor m )
        {
            throw new NotImplementedException();
        }

        public ValueTask<ushort> StoreMessageAsync( IActivityMonitor m, ApplicationMessage message, QualityOfService qos )
        {
            throw new NotImplementedException();
        }

        public ValueTask<bool> StorePacketIdAsync( IActivityMonitor m, ushort packetId )
        {
            throw new NotImplementedException();
        }
    }
}
