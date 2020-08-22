using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IPacketIdStore
    {
        ValueTask StoreId( IInputLogger? m, int id );

        ValueTask RemoveId( IInputLogger? m, int id );

        bool Empty { get; }

        ValueTask ResetAsync();
    }
}
