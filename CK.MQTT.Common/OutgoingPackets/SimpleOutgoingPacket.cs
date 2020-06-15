using CK.Core;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public abstract class SimpleOutgoingPacket : OutgoingPacket
    {
        protected abstract void Write( PipeWriter pw );

        public override ValueTask WriteAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            Write( pw );
            return pw.FlushAsync( cancellationToken ).AsNonGenericValueTask();
        }
    }
}
