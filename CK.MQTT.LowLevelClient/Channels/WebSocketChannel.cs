using CK.MQTT;
using Nerdbank.Streams;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class WebSocketChannel : IMqttChannel
    {
        readonly HttpListenerWebSocketContext _context;

        public WebSocketChannel( HttpListenerWebSocketContext context )
        {
            _context = context;
        }

        public ValueTask StartAsync( CancellationToken cancellationToken )
        {
            DuplexPipe = _context.WebSocket.UsePipe( cancellationToken: cancellationToken );
            return new ValueTask();
        }

        public bool IsConnected => _context.WebSocket.State == WebSocketState.Open;

        public IDuplexPipe? DuplexPipe { get; private set; }

        public async ValueTask CloseAsync(DisconnectReason reason)
        {
            await _context.WebSocket.CloseAsync( WebSocketCloseStatus.Empty, "todo-status-description", default );
        }

        public void Dispose() => _context.WebSocket.Dispose();
    }
}
