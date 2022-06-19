using System;
using System.ComponentModel;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server.ServerClient
{
    public class TcpListenerSingleChannel : IMqttChannel
    {
        // I feel this object is badly designed, as long as it's alive it block a port.
        readonly CancellationTokenSource _cts = new();
        readonly IPAddress _address;
        readonly int _port;
        readonly Task _backgroundTask;
        public TcpListenerSingleChannel( IPAddress address, int port )
        {
            _address = address;
            _port = port;
            _backgroundTask = BackgroundWorkerAsync();
        }

        TaskCompletionSource<TcpClient>? _tcs;
        TcpClient? _tcpClient; // when null, the channel is disconnected.
        NetworkStream? _stream;
        DuplexPipe? _duplexPipe;

        public bool IsConnected => _tcpClient?.Connected ?? false;

        public IDuplexPipe? DuplexPipe => _duplexPipe;

        public async Task BackgroundWorkerAsync()
        {
            var listener = new TcpListener( _address, _port );
            listener.Start();
            while( !_cts.IsCancellationRequested )
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync( _cts.Token );
                }
                catch( OperationCanceledException )
                {
                    break;
                }
                if( _cts.IsCancellationRequested ) break;
                var tcs = _tcs;
                if( tcs == null )
                {
                    client.Close();
                    client.Dispose();
                }
                else
                {
                    tcs.SetResult( client );
                    _tcs = null;
                }
            }
            listener.Stop();
        }

        public async ValueTask StartAsync( CancellationToken cancellationToken )
        {
            if( _tcpClient != null ) throw new InvalidOperationException( "Already connected." );
            var tcs = new TaskCompletionSource<TcpClient>();
            _tcs = tcs;
            _tcpClient = await tcs.Task.WaitAsync( cancellationToken );
            if( cancellationToken.IsCancellationRequested ) return;
            _stream = _tcpClient.GetStream();
            _duplexPipe = new DuplexPipe( PipeReader.Create( _stream ), PipeWriter.Create( _stream ) );
        }

        public void Close()
        {
            if( _tcpClient == null ) throw new InvalidOperationException( "Channel is not started." );
            _tcpClient.Close();
            _stream!.Dispose();
            _tcpClient = null;
        }

        public void Dispose()
        {
            _cts.Cancel();
            if( !_backgroundTask.IsCompleted ) throw new InvalidOperationException( "The background task should be completed" );
        }
    }
}
