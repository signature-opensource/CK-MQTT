using CK.MQTT;
using CK.MQTT.Server;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable.MQTT
{
    public class WebSocketChannelFactory : IMQTTChannelFactory
    {
        readonly HttpListener _httpListener;
        public WebSocketChannelFactory( int port ) : this( new[] { $"http://localhost:{port}/mqtt/" } )
        {
        }

        public WebSocketChannelFactory( IEnumerable<string> prefixes )
        {
            _httpListener = new();
            foreach( var prefix in prefixes )
            {
                _httpListener.Prefixes.Add( prefix );
            }
            _httpListener.Start();
        }

        public async ValueTask<(IMQTTChannel channel, string connectionInfo)> CreateAsync( CancellationToken cancellationToken )
        {
            var context = await _httpListener.GetContextAsync();
            var webSocketContext = await context.AcceptWebSocketAsync( "mqtt" );
            return (new WebSocketChannel( webSocketContext ), webSocketContext.User?.ToString() ?? "");
        }

        public void Dispose() => _httpListener.Stop();
    }
}
