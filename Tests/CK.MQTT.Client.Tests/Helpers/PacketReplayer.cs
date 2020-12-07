using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CK.Core;
using CK.Core.Extension;

namespace CK.MQTT.Client.Tests.Helpers
{
    /// <summary>
    /// Allow to test the client with weird packet without hacking a the server code.
    /// </summary>
    class PacketReplayer : IMqttChannelFactory
    {
        readonly Queue<TestPacket> _packets;
        public PacketReplayer( Queue<TestPacket> packets )
        {
            _packets = packets;
        }
        public TestDelayHandler TestDelayHandler { get; } = new();
        public Task? LastWorkTask { get; private set; }
        async Task WorkLoop( TestChannel testChannel )
        {
            while( _packets.Count > 0 )
            {
                TestPacket packet = _packets.Peek();
                if( packet.PacketDirection == PacketDirection.ToClient )
                {
                    await testChannel.TestDuplexPipe.Output.WriteAsync( packet.Buffer );
                }
                if( packet.PacketDirection == PacketDirection.ToServer )
                {
                    Memory<byte> buffer = new byte[packet.Buffer.Length];
                    PipeReaderExtensions.FillStatus status = await testChannel.TestDuplexPipe.Input.CopyToBuffer( buffer, default );
                    if( status != PipeReaderExtensions.FillStatus.Done ) throw new EndOfStreamException();
                    if( !buffer.Span.SequenceEqual( packet.Buffer.Span ) ) throw new InvalidDataException();
                }
                TestDelayHandler.IncrementTime( packet.OperationTime );
                _packets.Dequeue();
                await Task.Yield();
            }
        }

        public async ValueTask<IMqttChannel> CreateAsync( IActivityMonitor m, string connectionString )
        {
            if( LastWorkTask != null )
            {
                try
                {
                    await LastWorkTask;
                }
                catch( Exception )
                {

                }
            }
            TestChannel channel = new();
            LastWorkTask = WorkLoop( channel );
            return channel;
        }
    }
}
