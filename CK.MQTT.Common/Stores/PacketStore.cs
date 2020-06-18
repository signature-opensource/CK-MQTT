using CK.Core;
using CK.MQTT.Abstractions.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
    /// <summary>
    /// This class have no way to be closed by design.
    /// The implementation should guarantee the persitence when store methods are completed.
    /// If not, it WILL result in data loss.
    /// </summary>
    public abstract class PacketStore
    {
        readonly IdStore _packetStore;
        protected PacketStore( int packetIdMaxValue )
        {
            _packetStore = new IdStore( packetIdMaxValue );
        }

        /// <summary>
        /// Store a <see cref="IOutgoingPacketWithId"/> in the session, return a <see cref="IOutgoingPacket"/>.
        /// http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc442180912
        /// </summary>
        /// <returns>A <see cref="IOutgoingPacket"/> that can be sent on the wire.</returns>
        public async ValueTask<IOutgoingPacketWithId> StoreMessageAsync( IActivityMonitor m, IOutgoingPacketWithId packet )
        {
            int id = _packetStore.GetId();
            int waitTime = 500;
            while( id == 0 )
            {
                m.Warn( "No PacketID available, awaiting until one is free." );
                await Task.Delay( waitTime );
                if( waitTime < 5000 ) waitTime += 500;
                id = _packetStore.GetId();
            }
            packet.PacketId = (ushort)id;
            return await DoStoreMessageAsync( m, packet );
        }

        public Task<object?>? GetAwaiterById( int index ) => _packetStore.GetAwaiterById( index );

        protected abstract ValueTask<IOutgoingPacketWithId> DoStoreMessageAsync( IActivityMonitor m, IOutgoingPacketWithId packet );

        public async ValueTask<QualityOfService> DiscardMessageByIdAsync( IActivityMonitor m, int packetId, object? packet = null )
        {
            var qos = await DoDiscardMessage( m, packetId );
            if( qos == QualityOfService.AtLeastOnce )
            {
                _packetStore.FreeId( packetId, packet );
            }
            return qos;
        }

        protected abstract ValueTask<QualityOfService> DoDiscardMessage( IActivityMonitor m, int packetId );

        public async ValueTask DiscardPacketIdAsync( IActivityMonitor m, int packetId )
        {
            if( !_packetStore.FreeId( packetId ) )
            {
                m.Warn( "Freeing a packet that was already free." );
            }
            await DoDiscardPacketIdAsync( m, packetId );
            return;
        }

        protected abstract ValueTask DoDiscardPacketIdAsync( IActivityMonitor m, int packetId );
    }
}
