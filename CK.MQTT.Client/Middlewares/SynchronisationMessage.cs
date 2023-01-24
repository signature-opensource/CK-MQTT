using System.Threading.Tasks;

namespace CK.MQTT.Client.Middleware
{
    record SynchronizationMessage( TaskCompletionSource Tcs );
}
