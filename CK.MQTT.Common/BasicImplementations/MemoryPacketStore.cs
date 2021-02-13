using CK.Core;
using CK.MQTT.Common.OutgoingPackets;
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

            public void Dispose() => _disposable.Dispose();
        }

        /// <summary>
        /// Instantiates a new <see cref="MemoryPacketStore"/>.
        /// </summary>
        /// <param name="config">The config of the mqtt client.</param>
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

        protected override async ValueTask<IOutgoingPacket> DoStorePacket( IActivityMonitor? m, IOutgoingPacketWithId packet )
        {
            int packetSize = packet.GetSize( _protocolConfig.ProtocolLevel );
            m?.Trace( $"Renting {packetSize} bytes to persist {packet}." );
            IMemoryOwner<byte> memOwner = MemoryPool<byte>.Shared.Rent( packetSize );
            PipeWriter pipe = PipeWriter.Create( memOwner.Memory.AsStream() );//And write their content to this memory.
            if( await packet.WriteAsync( _protocolConfig.ProtocolLevel, pipe, default ) != WriteResult.Written ) throw new InvalidOperationException( "Didn't wrote packet correctly." );
            Memory<byte> slicedMem = memOwner.Memory.Slice( 0, packetSize );
            base[packet.PacketId].Content.Storage = new StoredPacket( slicedMem, memOwner );
            return new FromMemoryOutgoingPacket( slicedMem );
        }

        protected override ValueTask DoResetAsync()
        {
            TODO
        }

        public override IAsyncEnumerable<IOutgoingPacketWithId> RestoreAllPackets( IActivityMonitor? m )
        {
            throw new NotImplementedException();
        }

        protected override ValueTask<IOutgoingPacketWithId> RestorePacket( int packetId )
        {
            throw new NotImplementedException();
        }
    }
}
