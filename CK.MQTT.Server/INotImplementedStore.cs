using CK.MQTT.Stores;
using System;
using System.Collections.Generic;
using System.Text;
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
        ValueTask<(IMqttIdStore, IPacketIdStore)> GetStores( string addressId, string clientId );
    }
}
