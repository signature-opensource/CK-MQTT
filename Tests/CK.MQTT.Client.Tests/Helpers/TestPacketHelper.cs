using CK.Core;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    [ExcludeFromCodeCoverage]
    public static class TestPacketHelper
    {

        public static PacketReplayer.TestWorker SendToClient( ReadOnlyMemory<byte> data, TimeSpan operationTime = default ) =>
            async ( IActivityMonitor m, PacketReplayer replayer ) =>
            {
                using( m.OpenInfo( "Sending to client..." ) )
                {
                    await replayer.Channel!.TestDuplexPipe.Output.WriteAsync( data );
                    await replayer.Channel!.TestDuplexPipe.Output.FlushAsync();
                    replayer.TestDelayHandler.IncrementTime( operationTime );
                }
                return true;
            };

        public static PacketReplayer.TestWorker SendToClient( string hexArray, TimeSpan operationTime = default ) =>
            SendToClient( Convert.FromHexString( hexArray ), operationTime );

        public static PacketReplayer.TestWorker Outgoing( string hexArray, TimeSpan operationTime = default )
            => async ( IActivityMonitor m, PacketReplayer replayer ) =>
            {
                using( m.OpenInfo( "Outgoing packet..." ) )
                {
                    ReadOnlyMemory<byte> truthBuffer = Convert.FromHexString( hexArray );
                    Memory<byte> buffer = new byte[truthBuffer.Length];

                    using( CancellationTokenSource cts = Debugger.IsAttached ? new() : new( 500 ) )
                    {
                        ReadResult readResult = await replayer.Channel!.TestDuplexPipe.Input.ReadAtLeastAsync( buffer.Length, cts.Token );

                        if( cts.IsCancellationRequested || readResult.IsCanceled )
                        {
                            replayer.Channel!.TestDuplexPipe.Input.AdvanceTo( readResult.Buffer.Slice( Math.Min( buffer.Length, readResult.Buffer.Length ) ).End );
                            Assert.Fail( "Timeout." );
                        }
                        if( readResult.IsCompleted && readResult.Buffer.Length < buffer.Length )
                        {
                            replayer.Channel!.TestDuplexPipe.Input.AdvanceTo( readResult.Buffer.Slice( Math.Min( buffer.Length, readResult.Buffer.Length ) ).End );
                            Assert.Fail( "Partial data." );
                        }
                        ReadOnlySequence<byte> sliced = readResult.Buffer.Slice( 0, buffer.Length );

                        if( readResult.IsCompleted && readResult.Buffer.Length < buffer.Length ) Assert.Fail( "Partial read." );
                        sliced.CopyTo( buffer.Span );
                        replayer.Channel!.TestDuplexPipe.Input.AdvanceTo( sliced.End );

                        if( !buffer.Span.SequenceEqual( truthBuffer.Span ) ) Assert.Fail( "Buffer not equals." );
                    }
                    replayer.TestDelayHandler.IncrementTime( operationTime );
                }
                return true;
            };
        public static async ValueTask<bool> WaitClientDisconnect( IActivityMonitor m, PacketReplayer replayer )
        {
            using( m.OpenInfo( "Waiting for link to be down..." ) )
            {
                await replayer.Channel!.OnDisposeTask;
            }
            return false;
        }

        public static PacketReplayer.TestWorker SwallowEverything( CancellationToken stopToken )
            => async ( IActivityMonitor m, PacketReplayer replayer ) =>
            {
                using( m.OpenInfo( "Swallowing everything..." ) )
                {
                    await replayer.Channel!.TestDuplexPipe.Input.CopyToAsync( Stream.Null, stopToken );
                }
                return true;
            };

        public static ValueTask<bool> Disconnect( IActivityMonitor m, PacketReplayer replayer )
        {
            using( m.OpenInfo( "Disconnecting..." ) )
            {
                replayer.Channel!.Close( null );
                replayer.Channel.Dispose();
            }
            return new ValueTask<bool>( false );
        }

        public static PacketReplayer.TestWorker IncrementTime( TimeSpan timeToIncrement ) =>
            async ( IActivityMonitor m, PacketReplayer replayer ) =>
            {
                using( m.OpenInfo( "Incrementing time..." ) )
                {
                    replayer.TestDelayHandler.IncrementTime( timeToIncrement );
                }
                return true;
            };

        public static PacketReplayer.TestWorker Do( Func<IActivityMonitor, Task> func ) =>
            async ( IActivityMonitor m, PacketReplayer replayer ) =>
            {
                await func( m );
                return true;
            };
    }
}
