using CK.Core;
using CK.MQTT.Common.OutgoingPackets;
using CK.MQTT.Common.Stores;
using CK.MQTT.Stores;
using Microsoft.Toolkit.HighPerformance.Extensions;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CK.MQTT.IOutgoingPacket;

namespace CK.MQTT
{
    /// <summary>
    /// In memory implementation of<see cref="IPacketStore"/>.
    /// This class DONT persist the data !!!
    /// </summary>
    class MemoryPacketStore : MqttIdStore<MemoryPacketStore.StoredPacket>
    {
        readonly ProtocolConfiguration _protocolConfig;

        internal struct StoredPacket : IDisposable
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
        public MemoryPacketStore( ProtocolConfiguration protocolConfiguration, MqttConfigurationBase config, int packetIdMaxValue )
            : base( packetIdMaxValue, config )
        {
            _protocolConfig = protocolConfiguration;
        }

        /// <inheritdoc/>
        protected override ValueTask RemovePacketData( IInputLogger? m, ref StoredPacket storedPacket )
        {
            storedPacket.Dispose();
            storedPacket = default;
            return new ValueTask();
        }

        protected override async ValueTask<IOutgoingPacket> DoStorePacket( IActivityMonitor? m, IOutgoingPacket packet )
        {
            int packetSize = packet.GetSize( _protocolConfig.ProtocolLevel );
            m?.Trace( $"Renting {packetSize} bytes to persist {packet}." );
            IMemoryOwner<byte> memOwner = MemoryPool<byte>.Shared.Rent( packetSize );
            PipeWriter pipe = PipeWriter.Create( memOwner.Memory.AsStream() ); // And write their content to this memory.
            using( m?.OpenTrace( $"Serializing {packet} into memory..." ) )
            {
                if( await packet.WriteAsync( _protocolConfig.ProtocolLevel, pipe, default ) != WriteResult.Written ) throw new InvalidOperationException( "Didn't wrote packet correctly." );
            }
            Memory<byte> slicedMem = memOwner.Memory.Slice( 0, packetSize );
            base[packet.PacketId].Content.Storage = new StoredPacket( slicedMem, memOwner );
            return new FromMemoryOutgoingPacket( slicedMem, packet.Qos, packet.PacketId );
        }

        protected override ValueTask DoResetAsync( ArrayStartingAt1<IdStoreEntry<EntryContent>> entries )
        {
            for( int i = 1; i < entries.Length + 1; i++ )
            {
                entries[i].Content.Storage.Dispose();
            }
            return new ValueTask();
        }

        protected override ValueTask<IOutgoingPacket> RestorePacket( int packetId )
        {
            EntryContent content = base[packetId].Content;
            return new( new FromMemoryOutgoingPacket( content.Storage.Payload, (QualityOfService)(content._state & QoSState.QosMask), packetId ) );
        }
    }
}
