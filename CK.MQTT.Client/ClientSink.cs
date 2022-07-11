using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public class DefaultClientMessageSink : MqttMessageSink, IMqtt3ClientSink
    {
        internal int _manualCountRetry;
        public IMqtt3Client Client { get; set; } = null!; //set by the client.

        public record FailedManualConnect( ConnectResult connectResult, IMqtt3ClientSink.ManualConnectRetryBehavior behavior );
        public IMqtt3ClientSink.ManualConnectRetryBehavior OnFailedManualConnect( ConnectResult connectResult )
        {
            var behavior = connectResult.Status == ConnectStatus.ErrorUnrecoverable || _manualCountRetry++ >= 3
                    ? IMqtt3ClientSink.ManualConnectRetryBehavior.GiveUp
                    : IMqtt3ClientSink.ManualConnectRetryBehavior.Retry;
            Events.TryWrite( new FailedManualConnect( connectResult, behavior ) );
            return behavior;
        }

        public record ReconnectionFailed( ConnectResult ConnectResult );
        public virtual ValueTask<bool> OnReconnectionFailedAsync( ConnectResult result )
        {
            Events.TryWrite( new ReconnectionFailed( result ) );
            return new ValueTask<bool>( true );
        }
        public record Connected;
        public void OnConnected() => Events.TryWrite( new Connected() );
    }
}
