using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// <see langword="delegate"/> used when the client is Disconnected.
    /// </summary>
    /// <param name="reason">The reason of the disconnection.</param>
    /// <param name="selfReconnectTask">Not null when <see cref="ProtocolConfiguration"/>. after the connection has been lost </param>
    public delegate void Disconnected( DisconnectedReason reason, Task? selfReconnectTask );
}
