using System.Threading.Tasks;

namespace CK.MQTT.Stores
{
    /// <summary>
    /// Store packet identifier that were sent by the endpoint.
    /// </summary>
    /// <remarks>
    /// We don't use a <see cref="IOutgoingPacketStore"/> for incoming packet identified because they are not corellated to the locally generated packet identifier.
    /// </remarks>
    public interface IIncomingPacketStore
    {
        bool IsRevivedSession { get; set; }
        ValueTask StoreIdAsync( IInputLogger? m, int id );

        ValueTask RemoveIdAsync( IInputLogger? m, int id );

        bool Empty { get; }

        ValueTask ResetAsync();
    }
}
