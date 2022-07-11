using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public interface IMqtt3ClientSink : IMqtt3Sink
    {
        IMqtt3Client Client { get; set; }

        public enum ManualConnectRetryBehavior
        {
            GiveUp,
            Retry,
            YieldToBackground
        }

        ManualConnectRetryBehavior OnFailedManualConnect( ConnectResult connectResult );

        /// <returns><see langword="true"/> to try reconnecting. You can add delay logic to temporise a reconnection.</returns>
        ValueTask<bool> OnReconnectionFailedAsync( ConnectResult result );

        /// <summary>
        /// Called when the client is successfuly connected.
        /// </summary>
        void OnConnected();
    }
}
