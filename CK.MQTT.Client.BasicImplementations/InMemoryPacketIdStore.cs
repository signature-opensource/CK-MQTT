using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class InMemoryPacketIdStore : IPacketIdStore
    {
        readonly HashSet<int> _ids = new HashSet<int>();
        public ValueTask RemoveId( IMqttLogger m, int id )
        {
            _ids.Remove( id );
            return new ValueTask();
        }

        public ValueTask StoreId( IMqttLogger m, int id )
        {
            _ids.Add( id );
            return new ValueTask();
        }

        public ValueTask ResetAsync()
        {
            _ids.Clear();
            return new ValueTask();
        }

        public bool Empty => _ids.Count == 0;
    }
}
