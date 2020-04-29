using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace CK.MQTT.Common.Channels
{
    public class TcpStreamClient : IStreamClient
    {
        readonly TcpClient _client;

        public TcpStreamClient(TcpClient client)
        {
            _client = client;
        }
        public bool IsConnected => _client.Connected;

        public void Close() => _client.Close();

        public void Dispose() => _client.Dispose();
    }
}
