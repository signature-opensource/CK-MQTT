using System;
using System.Threading.Tasks;

namespace CK.MQTT.Stores;

/// <summary>
/// Store packet identifier that were sent by the endpoint.
/// </summary>
/// <remarks>
/// We don't use a <see cref="ILocalPacketStore"/> for incoming packet identified because they are not corellated to the locally generated packet identifier.
/// </remarks>
public interface IRemotePacketStore : IDisposable
{
    bool IsRevivedSession { get; set; }
    ValueTask StoreIdAsync( int id );

    ValueTask RemoveIdAsync( int id );

    bool Empty { get; }

    ValueTask ResetAsync();
}
