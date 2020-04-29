using CK.Core;
using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Channels
{
    public class TcpChannelFactory : IPacketChannelFactory
    {
        public async Task<IMqttChannel<IPacket>> CreateAsync( IActivityMonitor m, string connectionString )
        {
            var split = connectionString.Split( ":" );
            string hostname = split[0];
            int port = int.Parse( split[1] );
            TcpClient client = new TcpClient();
            await client.ConnectAsync( hostname, port );
            Memory<byte> workBuffer = new Memory<byte>( new byte[512] );//TODO: How to manage this buffer lifecycle ?
            return GenericChannel.Create( new GenericChannelStream( new TcpStreamClient( client ), client.GetStream() ), workBuffer );
        }
    }
}
