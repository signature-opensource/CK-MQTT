using CK.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Store packets, used by an <see cref="IMqtt3Client"/> to persist packets.
    /// This abstract class implement the packet id assignation, and delegate the actual job of persisting to the child classes.
    /// This class have no way to be closed/disposed, when a method complete, the packet should be persisted.
    /// If not, it WILL result in data loss. (Because of a power outage for exemple).
    /// </summary>
    public abstract class PacketStore
    {
        public IdStore IdStore { get; }

        protected PacketStore( ProtocolConfiguration pConfig, MqttConfigurationBase config, int packetIdMaxValue )
        {
            IdStore = new IdStore( packetIdMaxValue, config );
            PConfig = pConfig;
            Config = config;
        }

        protected ProtocolConfiguration PConfig { get; }

        protected MqttConfigurationBase Config { get; }

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
                return (Config.StoreTransformer.PacketTransformerOnSave( newPacket ), idFreedAwaiter);
            }
        }

        public async ValueTask<IAsyncEnumerable<IOutgoingPacketWithId>> GetAllMessagesAsync( IActivityMonitor m )
            => (await DoGetAllMessagesAsync( m )).Select( s => Config.StoreTransformer.PacketTransformerOnRestore( s ) );

        protected abstract ValueTask<IAsyncEnumerable<IOutgoingPacketWithId>> DoGetAllMessagesAsync( IActivityMonitor m );

        public async ValueTask<IOutgoingPacketWithId> GetMessageByIdAsync( IOutputLogger? m, int packetId )
            => Config.StoreTransformer.PacketTransformerOnRestore( await DoGetMessageByIdAsync( m, packetId ) );

        protected abstract ValueTask<IOutgoingPacketWithId> DoGetMessageByIdAsync( IOutputLogger? m, int packetId );

        protected abstract ValueTask<IOutgoingPacketWithId> DoStoreMessageAsync( IActivityMonitor m, IOutgoingPacketWithId packet );

        public async ValueTask<QualityOfService> DiscardMessageByIdAsync( IInputLogger? m, int packetId, object? packet = null )
        {
            var qos = await DoDiscardMessage( m, packetId );
            if( qos == QualityOfService.AtLeastOnce )
            {
                IdStore.FreeId( m, packetId, packet );
            }
            return qos;
        }

        protected abstract ValueTask<QualityOfService> DoDiscardMessage( IInputLogger? m, int packetId );

        internal async ValueTask DiscardPacketIdAsync( IInputLogger? m, int packetId )
        {
            if( !IdStore.FreeId( m, packetId ) )
            {
                m?.DoubleFreePacketId( packetId ); //TODO: maybe this should be a Protocol Error and disconnect ?
            }
            await DoDiscardPacketIdAsync( m, packetId );
            return;
        }

        protected abstract ValueTask DoDiscardPacketIdAsync( IInputLogger? m, int packetId );

        public ValueTask ResetAsync()
        {
            if( IdStore.Empty ) return new ValueTask();
            IdStore.Reset();
            return DoReset();
        }

        protected abstract ValueTask DoReset();
    }
}
