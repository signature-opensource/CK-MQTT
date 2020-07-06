using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// This class have no way to be closed by design.
    /// The implementation should guarantee the persitence when store methods are completed.
    /// If not, it WILL result in data loss.
    /// </summary>
    public abstract class PacketStore
    {
        public delegate IOutgoingPacketWithId PacketTransformer( IOutgoingPacketWithId packetToTransform );
        readonly IdStore _packetStore;
        readonly PacketTransformer _packetTransformerOnRestore;
        readonly PacketTransformer _packetTransformerOnSave;

        protected PacketStore( PacketTransformer packetTransformerOnRestore, PacketTransformer packetTransformerOnSave, int packetIdMaxValue )
        {
            _packetStore = new IdStore( packetIdMaxValue );
            _packetTransformerOnRestore = packetTransformerOnRestore;
            _packetTransformerOnSave = packetTransformerOnSave;
        }

        /// <summary>
        /// Store a <see cref="IOutgoingPacketWithId"/> in the session, return a <see cref="IOutgoingPacket"/>.
        /// </summary>
        /// <returns>A <see cref="IOutgoingPacket"/> that can be sent on the wire.</returns>
        public async ValueTask<(IOutgoingPacketWithId, Task<object?>)> StoreMessageAsync( IMqttLogger m, IOutgoingPacketWithId packet )
        {
            bool success = _packetStore.TryGetId( out int packetId, out Task<object?>? idFreedAwaiter );
            int waitTime = 500;
            while( !success )
            {
                m.Warn( "No PacketID available, awaiting until one is free." );
                await Task.Delay( waitTime );
                if( waitTime < 5000 ) waitTime += 500;
                success = _packetStore.TryGetId( out packetId, out idFreedAwaiter );
            }
            Debug.Assert( idFreedAwaiter != null );
            packet.PacketId = (ushort)packetId;
            var newPacket = await DoStoreMessageAsync( m, packet );
            return (_packetTransformerOnSave( newPacket ), idFreedAwaiter);
        }

        public async ValueTask<IOutgoingPacketWithId> GetMessageByIdAsync( IMqttLogger m, int packetId )
            => _packetTransformerOnRestore( await DoGetMessageByIdAsync( m, packetId ) );

        protected abstract ValueTask<IOutgoingPacketWithId> DoGetMessageByIdAsync( IMqttLogger m, int packetId );

        protected abstract ValueTask<IOutgoingPacketWithId> DoStoreMessageAsync( IMqttLogger m, IOutgoingPacketWithId packet );

        public async ValueTask<QualityOfService> DiscardMessageByIdAsync( IMqttLogger m, int packetId, object? packet = null )
        {
            var qos = await DoDiscardMessage( m, packetId );
            if( qos == QualityOfService.AtLeastOnce )
            {
                _packetStore.FreeId( packetId, packet );
            }
            return qos;
        }

        protected abstract ValueTask<QualityOfService> DoDiscardMessage( IMqttLogger m, int packetId );

        public async ValueTask DiscardPacketIdAsync( IMqttLogger m, int packetId )
        {
            if( !_packetStore.FreeId( packetId ) )
            {
                m.Warn( $"Freeing packet id {packetId} that was not assigned or already freed." );
            }
            await DoDiscardPacketIdAsync( m, packetId );
            return;
        }

        protected abstract ValueTask DoDiscardPacketIdAsync( IMqttLogger m, int packetId );

        public bool Empty => _packetStore.Empty;

        public ValueTask ResetAsync()
        {
            if( Empty ) return new ValueTask();
            _packetStore.Reset();
            return DoReset();
        }

        protected abstract ValueTask DoReset();
    }
}
