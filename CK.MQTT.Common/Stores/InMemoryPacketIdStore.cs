using CK.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
    public class InMemoryPacketIdStore : IPacketIdStore
    {
        readonly HashSet<int> _ids = new HashSet<int>();
        public ValueTask RemoveId( IActivityMonitor m, int id )
        {
            _ids.Remove( id );
            return new ValueTask();
        }

        public ValueTask StoreId( IActivityMonitor m, int id )
        {
            _ids.Add( id );
            return new ValueTask();
        }
    }
}
