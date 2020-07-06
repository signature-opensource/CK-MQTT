using CK.Core;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class VolatilePacketStoreManager : IPacketStoreManager
    {
        readonly int _packetIdMaxValue;

        public VolatilePacketStoreManager( int packetIdMaxValue )
        {
            _packetIdMaxValue = packetIdMaxValue;
        }
        public Task<PacketStore> CreateAsync( IMqttLogger m, string storeId, bool cleanSession )
            => Task.FromResult<PacketStore>( new MemoryPacketStore( _packetIdMaxValue ) );

        public Task<bool> DeleteAsync( IMqttLogger m, string storeId ) => Task.FromResult( true );
    }
}
