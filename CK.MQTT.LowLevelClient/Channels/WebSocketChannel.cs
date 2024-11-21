using Nerdbank.Streams;
using System;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT;

public class WebSocketChannel : IMQTTChannel
{
    readonly ClientWebSocket _client = new ClientWebSocket();
    readonly Uri _uri;

    public WebSocketChannel( Uri uri )
    {
        _uri = uri;
    }

    public async ValueTask StartAsync( CancellationToken cancellationToken )
    {
        await _client.ConnectAsync( _uri, cancellationToken );
        DuplexPipe = _client.UsePipe( cancellationToken: cancellationToken );
    }

    public bool IsConnected => _client.State == WebSocketState.Open;

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
                await _client.CloseAsync( WebSocketCloseStatus.NormalClosure, null, default );
                break;
            case DisconnectReason.ProtocolError:
                await _client.CloseAsync( WebSocketCloseStatus.ProtocolError, null, default );
                break;
            case DisconnectReason.InternalException:
                await _client.CloseAsync( WebSocketCloseStatus.InternalServerError, null, default );
                break;
            case DisconnectReason.Timeout:
                await _client.CloseAsync( WebSocketCloseStatus.EndpointUnavailable, "Timeout", default );
                break;
            default:
                throw new InvalidOperationException();
        }

    }

    public void Dispose() => _client.Dispose();
}
