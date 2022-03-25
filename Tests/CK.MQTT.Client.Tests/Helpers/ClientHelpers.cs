using CK.Core;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    public class ClientHelpers
    {
        public static ValueTask NotListening_Dispose( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken cancellationToken )
        {
            msg.Dispose();
            return new ValueTask();
        }

        public static ValueTask NotListening_New( IActivityMonitor? m, ApplicationMessage msg, CancellationToken cancellationToken )
        {
            return new ValueTask();
        }
    }
}
