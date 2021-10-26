using CK.Core;
using CK.Core.Extension;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    /// <summary>
    /// Allow to test the client with weird packet without hacking a the server code.
    /// </summary>
    public class PacketReplayer : IMqttChannelFactory
    {
        public Channel<TestWorker> PacketsWorker { get; }
        public TestChannel? Channel { get; private set; }
        public PacketReplayer( IEnumerable<TestWorker>? packets = null )
        {
            PacketsWorker = System.Threading.Channels.Channel.CreateUnbounded<TestWorker>();
            if( packets != null )
            {
                foreach( var item in packets )
                {
                    PacketsWorker.Writer.TryWrite( item );
                }
            }
        }
        public TestDelayHandler TestDelayHandler { get; } = new();
        Task? _workLoopTask;
        public async Task StopAndEnsureValidAsync()
        {
            PacketsWorker.Writer.Complete();
            Task? task = _workLoopTask;
            if( task != null ) await task;
            _workLoopTask?.IsCompletedSuccessfully.Should().BeTrue();
        }

        /// <summary>
        /// When <see langword="true"/>, drop all data.
        /// </summary>
        public int DropData { get; set; }
        public delegate ValueTask<bool> TestWorker( PacketReplayer packetReplayer );
        async Task WorkLoop()
        {
            await foreach( TestWorker func in PacketsWorker.Reader.ReadAllAsync() )
            {
                if( !await func( this ) ) break;
            }
            _workLoopTask = null;
        }

        public async ValueTask<IMqttChannel> CreateAsync( IActivityMonitor? m, string connectionString )
        {
            Channel = new();
            if( _workLoopTask != null )
            {
                if( Debugger.IsAttached )
                {
                    await _workLoopTask;
                }
                else
                {
                    await _workLoopTask.WaitAsync( 500 );
                }
                if( _workLoopTask != null ) throw new InvalidOperationException( "A work is already running." );
            }
            _workLoopTask = WorkLoop();
            return Channel;
        }
    }
}
