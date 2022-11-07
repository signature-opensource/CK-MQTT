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
    public class WebSocketChannel : IMQTTChannel
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

        public async ValueTask CloseAsync( DisconnectReason reason )
        {
            switch( reason )
            {
                case DisconnectReason.None:
                case DisconnectReason.RemoteDisconnected:
                    //TODO: Remote disconnected... Do I have to close the websocket ?
                    break;
                case DisconnectReason.UserDisconnected:
                    await _context.WebSocket.CloseAsync( WebSocketCloseStatus.NormalClosure, null, default );
                    break;
                case DisconnectReason.ProtocolError:
                    await _context.WebSocket.CloseAsync( WebSocketCloseStatus.ProtocolError, null, default );
                    break;
                case DisconnectReason.InternalException:
                    await _context.WebSocket.CloseAsync( WebSocketCloseStatus.InternalServerError, null, default );
                    break;
                case DisconnectReason.Timeout:
                    await _context.WebSocket.CloseAsync( WebSocketCloseStatus.EndpointUnavailable, "Timeout", default );
                    break;
                default:
                    throw new InvalidOperationException();
            }

        }

        public void Dispose() => _context.WebSocket.Dispose();
    }
}
