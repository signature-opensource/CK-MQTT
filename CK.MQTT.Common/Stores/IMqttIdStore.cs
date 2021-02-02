using CK.Core;
using System.Threading.Tasks;

namespace CK.MQTT.Stores
{
    public interface IMqttIdStore
    {
        ValueTask<Task<object?>> StoreMessageAsync( IActivityMonitor? m, IOutgoingPacketWithId packet, QualityOfService qos );
        void OnPacketSent( IOutputLogger? m, int packetId );
        ValueTask OnQos1AckAsync( IInputLogger? m, int packetId, object? result );
        ValueTask OnQos2AckStep1Async( IInputLogger? m, int packetId );
        void OnQos2AckStep2( IInputLogger? m, int packetId );
    }
}
