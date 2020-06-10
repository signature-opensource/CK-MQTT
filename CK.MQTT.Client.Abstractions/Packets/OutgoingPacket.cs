using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public abstract class OutgoingPacket
    {
        protected abstract PacketType PacketType { get; }
        internal protected abstract ValueTask WriteAsync( PipeWriter writer, CancellationToken cancellationToken );
    }
}
