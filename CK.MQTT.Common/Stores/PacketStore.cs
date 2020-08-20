using CK.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Store packets, used by an <see cref="IMqttClient"/> to persist packets.
    /// This abstract class implement the packet id assignation, and delegate the actual job of persisting to the child classes.
    /// This class have no way to be closed/disposed, when a method complete, the packet should be persisted.
    /// If not, it WILL result in data loss. (Because of a power outage for exemple).
    /// </summary>
    public abstract class PacketStore
    {
        internal IdStore IdStore { get; }
        readonly MqttConfiguration _config;

        protected PacketStore( MqttConfiguration config, int packetIdMaxValue )
        {
            IdStore = new IdStore( packetIdMaxValue, config );
            _config = config;
        }

        /// <summary>
        /// Store a <see cref="IOutgoingPacketWithId"/> in the session, return a <see cref="IOutgoingPacket"/>.
        /// </summary>
        /// <returns>A <see cref="IOutgoingPacket"/> that can be sent on the wire.</returns>
        internal async ValueTask<(IOutgoingPacketWithId, Task<object?>)> StoreMessageAsync( IActivityMonitor m, IOutgoingPacketWithId packet )
        {
            bool success = IdStore.TryGetId( out int packetId, out Task<object?>? idFreedAwaiter );
            int waitTime = 500;
            while( !success )
            {
                m.Warn()?.Send( "No PacketId available, awaiting until one is free." );
                await Task.Delay( waitTime );
                if( waitTime < 5000 ) waitTime += 500;
                success = IdStore.TryGetId( out packetId, out idFreedAwaiter );
            }
            Debug.Assert( idFreedAwaiter != null );
            packet.PacketId = (ushort)packetId;
            using( m.OpenTrace()?.Send( $"{nameof( IdStore )} determined new packet id would be {packetId}." ) )
            {
                var newPacket = await DoStoreMessageAsync( m, packet );
                return (_config.StoreTransformer.PacketTransformerOnSave( newPacket ), idFreedAwaiter);
            }
        }

        public async ValueTask<IAsyncEnumerable<IOutgoingPacketWithId>> GetAllMessagesAsync( IActivityMonitor m )
            => (await DoGetAllMessagesAsync( m )).Select( s => _config.StoreTransformer.PacketTransformerOnRestore( s ) );

        protected abstract ValueTask<IAsyncEnumerable<IOutgoingPacketWithId>> DoGetAllMessagesAsync( IActivityMonitor m );

        internal async ValueTask<IOutgoingPacketWithId> GetMessageByIdAsync( IMqttLogger m, int packetId )
            => _config.StoreTransformer.PacketTransformerOnRestore( await DoGetMessageByIdAsync( m, packetId ) );

        protected abstract ValueTask<IOutgoingPacketWithId> DoGetMessageByIdAsync( IMqttLogger m, int packetId );

        protected abstract ValueTask<IOutgoingPacketWithId> DoStoreMessageAsync( IActivityMonitor m, IOutgoingPacketWithId packet );

        public async ValueTask<QualityOfService> DiscardMessageByIdAsync( IMqttLogger m, int packetId, object? packet = null )
        {
            var qos = await DoDiscardMessage( m, packetId );
            if( qos == QualityOfService.AtLeastOnce )
            {
                IdStore.FreeId( packetId, packet );
            }
            return qos;
        }

        protected abstract ValueTask<QualityOfService> DoDiscardMessage( IMqttLogger m, int packetId );

        internal async ValueTask DiscardPacketIdAsync( IMqttLogger m, int packetId )
        {
            if( !IdStore.FreeId( packetId ) )
            {
                m.Warn( $"Freeing packet id {packetId} that was not assigned or already freed." );
            }
            await DoDiscardPacketIdAsync( m, packetId );
            return;
        }

        protected abstract ValueTask DoDiscardPacketIdAsync( IMqttLogger m, int packetId );

        public ValueTask ResetAsync()
        {
            if( IdStore.Empty ) return new ValueTask();
            IdStore.Reset();
            return DoReset();
        }

        protected abstract ValueTask DoReset();
    }
}
