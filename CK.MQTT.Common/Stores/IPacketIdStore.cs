using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IPacketIdStore
    {
        ValueTask StoreId( IMqttLogger m, int id );

        ValueTask RemoveId( IMqttLogger m, int id );

        bool Empty { get; }

        ValueTask ResetAsync();
    }
}
