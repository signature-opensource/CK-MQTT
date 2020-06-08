using CK.Core;
using CK.MQTT.Common.Packets;
using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public abstract class SimpleOutgoingPacket : OutgoingPacket
    {
        protected abstract void Write( PipeWriter pw );

        protected override ValueTask WriteAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            Write( pw );
            return pw.FlushAsync( cancellationToken ).AsNonGenericValueTask();
        }
    }
}
