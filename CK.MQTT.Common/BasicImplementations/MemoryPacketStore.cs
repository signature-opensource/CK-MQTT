using CK.MQTT.Common.Stores;
using CK.MQTT.Stores;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading.Tasks;
using CK.MQTT.Packets;
using CommunityToolkit.HighPerformance;

namespace CK.MQTT
{
    /// <summary>
    /// In memory implementation of<see cref="IPacketStore"/>.
    /// This class DONT persist the data !!!
    /// </summary>
    public class MemoryPacketStore : MqttIdStore<MemoryPacketStore.StoredPacket>
    {
        public struct StoredPacket : IDisposable
        {
            public readonly ReadOnlyMemory<byte> Payload;
            readonly IDisposable _disposable;

            public StoredPacket( ReadOnlyMemory<byte> payload, IDisposable disposable )
            {
                Payload = payload;
                _disposable = disposable;
            }
            public void Dispose() => _disposable?.Dispose(); // _disposable may be null when the struct is default.
        }

        /// <summary>
        /// Instantiates a new <see cref="MemoryPacketStore"/>.
        /// </summary>
        /// <param name="config">The configuration of the mqtt client.</param>
        /// <param name="packetIdMaxValue">The maximum id supported by the protocol.</param>
        public MemoryPacketStore( Mqtt3ConfigurationBase config, ushort packetIdMaxValue )
            : base( packetIdMaxValue, config )
        {
        }

        /// <inheritdoc/>
        protected override ValueTask RemovePacketDataAsync( ref StoredPacket storedPacket )
        {
            storedPacket.Dispose();
            storedPacket = default;
            return new ValueTask();
        }

        protected override async ValueTask<IOutgoingPacket> DoStorePacketAsync( IOutgoingPacket packet )
        {
            uint packetSize = packet.GetSize( Config.ProtocolConfiguration.ProtocolLevel );
            IMemoryOwner<byte> memOwner = MemoryPool<byte>.Shared.Rent( (int)packetSize );
            PipeWriter pipe = PipeWriter.Create( memOwner.Memory.AsStream() ); // And write their content to this memory.
            if( await packet.WriteAsync( Config.ProtocolConfiguration.ProtocolLevel, pipe, default ) != WriteResult.Written ) throw new InvalidOperationException( "Didn't wrote packet correctly." );
            Memory<byte> slicedMem = memOwner.Memory.Slice( 0, (int)packetSize );
            base[packet.PacketId].Content.Storage = new StoredPacket( slicedMem, memOwner );
            return new FromMemoryOutgoingPacket( slicedMem, packet.Qos, packet.PacketId, packet.IsRemoteOwnedPacketId );
        }

        protected override ValueTask DoResetAsync( ArrayStartingAt1<IdStoreEntry<EntryContent>> entries )
        {
            for( int i = 1; i < entries.Length + 1; i++ )
            {
                entries[i].Content.Storage.Dispose();
            }
            return new ValueTask();
        }

        protected override ValueTask<IOutgoingPacket> RestorePacketAsync( ushort packetId )
        {
            EntryContent content = base[packetId].Content;
            Debug.Assert( content.Storage.Payload.Length > 0 );
            return new( new FromMemoryOutgoingPacket( content.Storage.Payload, (QualityOfService)(content._state & QoSState.QosMask), packetId, false ) );
        }

        protected async override ValueTask<IOutgoingPacket> OverwriteMessageAsync( IOutgoingPacket packet )
        {
            base[packet.PacketId].Content.Storage.Dispose();
            uint packetSize = packet.GetSize( Config.ProtocolConfiguration.ProtocolLevel );
            IMemoryOwner<byte> memOwner = MemoryPool<byte>.Shared.Rent( (int)packetSize );
            PipeWriter pipe = PipeWriter.Create( memOwner.Memory.AsStream() ); // And write their content to this memory.
            if( await packet.WriteAsync( Config.ProtocolConfiguration.ProtocolLevel, pipe, default ) != WriteResult.Written ) throw new InvalidOperationException( "Didn't wrote packet correctly." );
            Memory<byte> slicedMem = memOwner.Memory.Slice( 0, (int)packetSize );
            base[packet.PacketId].Content.Storage = new StoredPacket( slicedMem, memOwner );
            return new FromMemoryOutgoingPacket( slicedMem, packet.Qos, packet.PacketId, packet.IsRemoteOwnedPacketId );
        }
    }
}
