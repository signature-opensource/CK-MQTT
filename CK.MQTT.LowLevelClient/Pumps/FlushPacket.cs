﻿using CK.MQTT.Packets;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Pumps
{
    /// <summary>
    /// Packet used to wake up the wait
    /// This packet will never be published.
    /// </summary>
    class FlushPacket : IOutgoingPacket
    {
        public static IOutgoingPacket Instance { get; } = new FlushPacket();
        private FlushPacket() { }
        public ushort PacketId { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public QualityOfService Qos => throw new NotSupportedException();

        public bool IsRemoteOwnedPacketId => throw new NotSupportedException();

        public uint GetSize( ProtocolLevel protocolLevel ) => throw new NotSupportedException();

        public ValueTask<WriteResult> WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken ) => throw new NotSupportedException();
    }
}