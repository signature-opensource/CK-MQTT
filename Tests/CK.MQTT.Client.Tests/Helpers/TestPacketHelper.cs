using CK.Core;
using CK.Core.Extension;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    public static class TestPacketHelper
    {

        public static PacketReplayer.TestWorker SendToClient( string hexArray, TimeSpan operationTime = default ) =>
            async ( IActivityMonitor m, PacketReplayer replayer ) =>
            {
                using( m.OpenInfo( "Sending to client..." ) )
                {
                    await replayer.Channel!.TestDuplexPipe.Output.WriteAsync( FromString( hexArray ) );
                    await replayer.Channel!.TestDuplexPipe.Output.FlushAsync();
                    replayer.TestDelayHandler.IncrementTime( operationTime );
                }
                return true;
            };
        public static PacketReplayer.TestWorker Outgoing( string hexArray, TimeSpan operationTime = default )
            => async ( IActivityMonitor m, PacketReplayer replayer ) =>
            {
                using( m.OpenInfo( "Outgoing packet..." ) )
                {
                    ReadOnlyMemory<byte> truthBuffer = FromString( hexArray );
                    Memory<byte> buffer = new byte[truthBuffer.Length];

                    using( CancellationTokenSource cts = Debugger.IsAttached ? new() : new( 500 ) )
                    {
                        PipeReaderExtensions.FillStatus status = await replayer.Channel!.TestDuplexPipe.Input.CopyToBufferAsync( buffer, cts.Token );
                        if( cts.IsCancellationRequested ) Assert.Fail( "Timeout." );
                        if( status != PipeReaderExtensions.FillStatus.Done ) Assert.Fail( "Fill status not ok." );
                        if( !buffer.Span.SequenceEqual( truthBuffer.Span ) ) Assert.Fail( "Buffer not equals." );
                    }
                    replayer.TestDelayHandler.IncrementTime( operationTime );
                }
                return true;
            };
        public static async ValueTask<bool> LinkDown( IActivityMonitor m, PacketReplayer replayer )
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

        public static ReadOnlyMemory<byte> FromString( string hexArray )
        {
            hexArray = hexArray.ToLower();
            byte[] arr = new byte[(hexArray.Length / 2)];
            for( int i = 0; i < hexArray.Length; i += 2 )
            {
                arr[i / 2] = Convert.ToByte( hexArray.Substring( i, 2 ), 16 );
            }
            return arr;
        }
    }
}
