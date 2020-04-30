using CK.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
    public interface IPacketStore
    {
        /// <summary>
        /// Store a message in the session, return a packet identifier to use in the QoS flow.
        /// http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc442180912
        /// </summary>
        /// <returns>An unused packet identifier</returns>
        ValueTask<ushort> StoreMessageAsync( IActivityMonitor m, ApplicationMessage message, QualityOfService qos );

        /// <summary>
        /// Discard a message. The packet ID will be freed if the QoS of the stored message is <see cref="QualityOfService.AtMostOnce"/>.
        /// </summary>
        /// <param name="packetId">The packet ID of the message to discard.</param>
        /// <param name="freePacketId"></param>
        /// <returns><see langword="true"/> if the packet ID have been freed (QoS at least once).</returns>
        ValueTask<QualityOfService> DiscardMessageFromIdAsync( IActivityMonitor m, ushort packetId );

        IEnumerable<StoredApplicationMessage> AllStoredMessages { get; }

        IEnumerable<ushort> OrphansPacketsId { get; }

        ValueTask<ushort> GetNewPacketId( IActivityMonitor m );

        ValueTask<bool> StorePacketIdAsync( IActivityMonitor m, ushort packetId );

        ValueTask<bool> FreePacketIdAsync( IActivityMonitor m, ushort packetId );


        Task CloseAsync( IActivityMonitor m );

    }

    public readonly struct StoredApplicationMessage
    {
        public StoredApplicationMessage( ApplicationMessage applicationMessage, QualityOfService qualityOfService, ushort packetId )
        {
            ApplicationMessage = applicationMessage;
            QualityOfService = qualityOfService;
            PacketId = packetId;
        }
        public readonly ApplicationMessage ApplicationMessage;
        public readonly QualityOfService QualityOfService;
        public readonly ushort PacketId;
    }
}
