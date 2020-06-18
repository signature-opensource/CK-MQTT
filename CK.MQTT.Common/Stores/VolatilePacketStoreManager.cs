using CK.Core;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
    public class VolatilePacketStoreManager : IPacketStoreManager
    {
        readonly int _packetIdMaxValue;

        public VolatilePacketStoreManager( int packetIdMaxValue )
        {
            _packetIdMaxValue = packetIdMaxValue;
        }
        public Task<PacketStore> CreateAsync( IActivityMonitor m, string storeId, bool cleanSession )
            => Task.FromResult<PacketStore>( new MemoryPacketStore( _packetIdMaxValue ) );

        public Task<bool> DeleteAsync( IActivityMonitor m, string storeId ) => Task.FromResult( true );
    }
}
