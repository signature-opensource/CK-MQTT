using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
    /// <summary>
    /// This interface have no way to be closed by design.
    /// The implementation should guarantee the persitence when store methods are completed.
    /// If not, it WILL result in data loss.
    /// </summary>
    public interface IPacketStore
    {
        /// <summary>
        /// Store a message in the session, return a packet identifier to use in the QoS flow.
        /// http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc442180912
        /// </summary>
        /// <returns>An unused packet identifier</returns>
        ValueTask<ushort> StoreMessageAsync( IActivityMonitor m, string topic, int payloadLength, Func<Stream, ValueTask> payload, QualityOfService qos );

        /// <summary>
        /// Discard a message. The packet ID will be freed if the QoS of the stored message is <see cref="QualityOfService.AtMostOnce"/>.
        /// </summary>
        /// <param name="packetId">The packet ID of the message to discard.</param>
        ValueTask<QualityOfService> DiscardMessageByIdAsync( IActivityMonitor m, ushort packetId );

        IDictionary<ushort, bool> AllStoredId { get; }

        IEnumerable<ushort> OrphansPacketsId { get; }

        ValueTask<ushort> GetNewPacketId( IActivityMonitor m );

        ValueTask<bool> StorePacketIdAsync( IActivityMonitor m, ushort packetId );

        ValueTask<bool> FreePacketIdAsync( IActivityMonitor m, ushort packetId );
    }
}
