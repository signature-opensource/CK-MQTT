using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CK.Core.Extension;

namespace CK.MQTT.Client.Tests.Helpers
{
    /// <summary>
    /// Allow to test the client with weird packet without hacking a the server code.
    /// </summary>
    class PacketReplayer
    {
        public enum PacketDirection
        {
            ToServer,
            ToClient
        }
        public struct TestPacket
        {
            public ReadOnlyMemory<byte> Buffer;
            public PacketDirection PacketDirection;
            public TestPacket( ReadOnlyMemory<byte> buffer, PacketDirection packetDirection )
            {
                Buffer = buffer;
                PacketDirection = packetDirection;
            }

        }

        readonly TestChannel _testChannel;
        readonly IAsyncEnumerable<TestPacket> _packets;
        readonly Task _workLoop;
        public PacketReplayer( TestChannel testChannel, IAsyncEnumerable<TestPacket> packets )
        {
            _testChannel = testChannel;
            _packets = packets;
            _workLoop = WorkLoop();
        }

        async Task WorkLoop()
        {
            await foreach( TestPacket packet in _packets )
            {
                if( packet.PacketDirection == PacketDirection.ToClient )
                {
                    await _testChannel.TestDuplexPipe.Output.WriteAsync( packet.Buffer );
                }
                if( packet.PacketDirection == PacketDirection.ToServer )
                {
                    int readCount = 0;
                    while( readCount < packet.Buffer.Length )
                    {
                        Memory<byte> buffer = new byte[packet.Buffer.Length];
                        var res = await _testChannel.TestDuplexPipe.Input.CopyToBuffer( buffer, default );
                        if( res == PipeReaderExtensions.FillStatus.Canceled ) throw new OperationCanceledException();
                        if(res == PipeReaderExtensions.FillStatus.Done )
                    }
                }
            }
        }

    }
}
