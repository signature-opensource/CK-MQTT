using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public abstract class MqttPacketStoreManager
    {
        readonly IPacketStoreFactory _store;

        public MqttPacketStoreManager( IPacketStoreFactory store )
        {
            _store = store;
        }

        static IOutgoingPacketWithId PacketTransformer( IOutgoingPacketWithId packetToTransform )
        {
            
        }

        public Task<PacketStore> CreateAsync( IMqttLogger m, string storeId, bool cleanSession )
        {
            _store.CreateAsync( m );
        }

        public Task<bool> DeleteAsync( IMqttLogger m, string storeId )
        {
            throw new NotImplementedException();
        }
    }
}
