using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace CK.MQTT.Common.Channels
{
    public class TcpChannel : IMqttChannel
    {
        readonly TcpClient _tcpClient;

        public TcpChannel( TcpClient tcpClient )
        {
            _tcpClient = tcpClient;
        }

        public bool IsConnected => _tcpClient.Connected;

        public Stream Stream => _tcpClient.GetStream();

        public void Close( IActivityMonitor m ) => _tcpClient.Close();

        public void Dispose() => _tcpClient.Dispose();
    }
}
