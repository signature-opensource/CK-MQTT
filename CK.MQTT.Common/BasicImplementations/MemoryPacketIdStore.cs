using CK.MQTT.Stores;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// In memory implementation of <see cref="IRemotePacketStore"/>. Does not persist data. Use this only if you can allow data loss on process crash (bug, power failure for exemple).
    /// </summary>
    public class MemoryPacketIdStore : IRemotePacketStore
    {
        readonly HashSet<int> _ids = new();

        public bool IsRevivedSession { get; set; }

        /// <inheritdoc/>
        public ValueTask RemoveIdAsync( IInputLogger? m, int id )
        {
            _ids.Remove( id );
            return new ValueTask();
        }

        /// <inheritdoc/>
        public ValueTask StoreIdAsync( IInputLogger? m, int id )
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
