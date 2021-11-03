using CK.MQTT.Stores;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    interface IServerStoreFactory
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="addressId">Address ID should be used to detect that the client changed location.
        /// may be used with strict security.</param>
        /// <param name="clientId"></param>
        /// <returns></returns>
        ValueTask<(IOutgoingPacketStore, IIncomingPacketStore)> GetStores( string addressId, string clientId );
    }
}
