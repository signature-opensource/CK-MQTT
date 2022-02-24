using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    /// <summary>
    /// Allow to test the client with weird packet without hacking a the server code.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class PacketReplayer : IMqttChannelFactory
    {
        public Channel<TestWorker> PacketsWorker { get; }
        public LoopBack? Channel { get; private set; }
        public PacketReplayer( string channelType, IEnumerable<TestWorker>? packets = null )
        {
            PacketsWorker = System.Threading.Channels.Channel.CreateUnbounded<TestWorker>();
            if( packets != null )
            {
                foreach( var item in packets )
                {
                    PacketsWorker.Writer.TryWrite( item );
                }
            }
            ChannelType = channelType;
        }
        public TestDelayHandler TestDelayHandler { get; } = new();
        public string ChannelType { get; set; }

        Task _workLoopTask = Task.CompletedTask;
        public async Task StopAndEnsureValidAsync()
        {
            PacketsWorker.Writer.Complete();
            Task task = _workLoopTask;
            if( !await task.WaitAsync( 50000 ) )
            {
                Assert.Fail( "Packet replayer didn't stopped in time." );
            }
            if( _workLoopTask.IsFaulted ) await _workLoopTask;
            _workLoopTask?.IsCompletedSuccessfully.Should().BeTrue();
            PacketsWorker.Reader.Count.Should().Be( 0 );
        }

        public delegate ValueTask<bool> TestWorker( IActivityMonitor m, PacketReplayer packetReplayer );

        readonly ActivityMonitor _m = new();
        async Task WorkLoop()
        {
            int i = 0;
            await foreach( TestWorker func in PacketsWorker.Reader.ReadAllAsync() )
            {
                using( _m.OpenInfo( $"Running test worker {i++}." ) )
                {
                    if( !await func( _m, this ) ) break;
                }
            }
        }

        public async ValueTask<IMqttChannel> CreateAsync( IActivityMonitor? m, string connectionString )
        {
            Task? task = _workLoopTask; //Capturing reference to avoid concurrency issue
            if( task != null )
            {
                await task;
                if( !_workLoopTask.IsCompleted ) throw new InvalidOperationException( "A work is already running." );
            }
            // This must be done after the wait. The work in the loop may use the channel.
            Channel = ChannelType switch
            {
                "Default" => new DefaultLoopback(),
                "BytePerByte" => new BytePerByteLoopback(),
                "PipeReaderCop" => new PipeReaderCopLoopback(),
                _ => throw new InvalidOperationException( "Unknown channel type." )
            };
            _workLoopTask = WorkLoop();
            return Channel;
        }
    }
}
