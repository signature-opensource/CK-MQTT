using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// In memory implementation of <see cref="IPacketIdStore"/>. Does not persist data. Use this only if you can allow data loss on process crash (bug, power failure for exemple).
    /// </summary>
    public class MemoryPacketIdStore : IPacketIdStore
    {
        readonly HashSet<int> _ids = new HashSet<int>();

        /// <inheritdoc/>
        public ValueTask RemoveId( IMqttLogger m, int id )
        {
            _ids.Remove( id );
            return new ValueTask();
        }

        /// <inheritdoc/>
        public ValueTask StoreId( IMqttLogger m, int id )
        {
            _ids.Add( id );
            return new ValueTask();
        }

        /// <inheritdoc/>
        public ValueTask ResetAsync()
        {
            _ids.Clear();
            return new ValueTask();
        }

        /// <inheritdoc/>
        public bool Empty => _ids.Count == 0;
    }
}
