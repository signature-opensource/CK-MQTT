using CK.Core;
using CK.Core.Extension;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    /// <summary>
    /// Allow to test the client with weird packet without hacking a the server code.
    /// </summary>
    class PacketReplayer : IMqttChannelFactory
    {
        readonly Queue<TestPacket> _packets;
        readonly bool _manualMode;
        TestChannel? _channel;
        public PacketReplayer( Queue<TestPacket> packets, bool manualMode = false )
        {
            _packets = packets;
            _manualMode = manualMode;
        }
        public TestDelayHandler TestDelayHandler { get; } = new();
        public Task? LastWorkTask { get; private set; }
        public async Task NextAsync( Task writingTask )
        {
            if( !_manualMode ) throw new InvalidOperationException( "Cannot move manually when not started in manual mode." );
            await Task.WhenAll( WorkLoop(), writingTask );
        }
        async Task WorkLoop()
        {
            while( _packets.Count > 0 )
            {
                TestPacket packet = _packets.Peek();
                if( packet.Buffer.Length > 0 )
                {

                    if( packet.PacketDirection == PacketDirection.ToClient )
                    {
                        await _channel!.TestDuplexPipe.Output.WriteAsync( packet.Buffer );
                    }
                    if( packet.PacketDirection == PacketDirection.ToServer )
                    {
                        Memory<byte> buffer = new byte[packet.Buffer.Length];
                        PipeReaderExtensions.FillStatus status = await _channel!.TestDuplexPipe.Input.CopyToBufferAsync( buffer, default );
                        if( status != PipeReaderExtensions.FillStatus.Done ) throw new EndOfStreamException();
                        if( !buffer.Span.SequenceEqual( packet.Buffer.Span ) ) throw new InvalidDataException();
                    }
                }
                TestDelayHandler.IncrementTime( packet.OperationTime );
                _packets.Dequeue();
                if( _manualMode ) return;
            }
        }

        public async ValueTask<IMqttChannel> CreateAsync( IActivityMonitor? m, string connectionString )
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
            _channel = new();
            if( !_manualMode ) LastWorkTask = WorkLoop();
            return _channel;
        }
    }
}
