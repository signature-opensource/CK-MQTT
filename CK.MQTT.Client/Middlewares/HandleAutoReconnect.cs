using CK.Core;
using CK.MQTT.Client.Middleware;
using CK.MQTT.LowLevelClient.Time;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CK.MQTT.Client.MQTTMessageSink;

namespace CK.MQTT.Client.Middleware
{
    public class HandleAutoReconnect : IAgentMessageMiddleware
    {
        Task _autoReconnect = Task.CompletedTask;
        readonly CancellationTokenSource _cts = new();
        readonly ITimeUtilities _timeUtilities;
        readonly IMQTT3Client _client;
        readonly ChannelWriter<object?> _writer;
        readonly Func<TimeSpan, TimeSpan> _shouldRetry;

        public HandleAutoReconnect( ITimeUtilities timeUtilities, IMQTT3Client client, ChannelWriter<object?> writer, Func<TimeSpan, TimeSpan> shouldRetry )
        {
            _timeUtilities = timeUtilities;
            _client = client;
            _writer = writer;
            _shouldRetry = shouldRetry;
        }

        public async ValueTask<bool> HandleAsync( IActivityMonitor m, object? message )
        {
            switch( message )
            {
                case UserDisconnect:
                    _cts.Cancel();
                    await _autoReconnect;
                    return false;
                case UnattendedDisconnect disconnect:
                    if( disconnect.Reason == DisconnectReason.UserDisconnected ) return false;
                    var isTimeout = await _autoReconnect.WaitAsync( 500 );
                    Debug.Assert( isTimeout );
                    _autoReconnect = BackgroundConnectWithRetriesAsync();
                    return false; // We don't want to swallow the message here.
                default:
                    return false;
            }
        }

        public record AutoReconnectAttempt();

        async Task BackgroundConnectWithRetriesAsync()
        {
            while( true )
            {
                await _writer.WriteAsync( new AutoReconnectAttempt() );
                var now = _timeUtilities.UtcNow;
                var res = await _client.ConnectAsync( false, _cts.Token );
                if( res.Status == ConnectStatus.Successful ) break;
                var timeUntilRetry = _shouldRetry( _timeUtilities.UtcNow - now );
                if( timeUntilRetry == Timeout.InfiniteTimeSpan) break;
                await _timeUtilities.Delay( timeUntilRetry );
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            await _autoReconnect;
        }
    }
}
