using CK.Core;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public abstract class SimpleOutgoingPacket : IOutgoingPacket
    {
        protected abstract void Write( PipeWriter pw );

        public ValueTask WriteAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            Write( pw );
            return pw.FlushAsync( cancellationToken ).AsNonGenericValueTask();
        }
        public abstract int GetSize();
    }
}
