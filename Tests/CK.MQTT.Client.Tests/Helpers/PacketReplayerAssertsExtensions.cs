using CK.Core;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;
namespace CK.MQTT.Client.Tests.Helpers
{
    static class PacketReplayerAssertsExtensions
    {
        public static TestMqttClient CreateMQTT3Client( this PacketReplayer replayer, Mqtt3ClientConfiguration config )
            => new( ProtocolConfiguration.Mqtt3, config, replayer.CreateChannel(), replayer.Events );

        public static TestMqttClient CreateMQTT5Client( this PacketReplayer replayer, ProtocolConfiguration pConfig, Mqtt5ClientConfiguration config )
            => new( ProtocolConfiguration.Mqtt5, config, replayer.CreateChannel(), replayer.Events );

        public static async Task AssertClientSent( this PacketReplayer @this, IActivityMonitor m, string hexArray )
        {
            using( m.OpenInfo( "Outgoing packet..." ) )
            {
                ReadOnlyMemory<byte> truthBuffer = Convert.FromHexString( hexArray );
                Memory<byte> buffer = new byte[truthBuffer.Length];

                using( CancellationTokenSource cts = Debugger.IsAttached ? new() : new( 500 ) )
                {
                    var testPipe = await @this.Channel!.GetTestDuplexPipe();
                    ReadResult readResult = await testPipe.Input.ReadAtLeastAsync( buffer.Length, cts.Token );

                    if( cts.IsCancellationRequested || readResult.IsCanceled ) Assert.Fail( "Timeout." );
                    if( readResult.IsCompleted && readResult.Buffer.Length < buffer.Length ) Assert.Fail( "Partial data." );

                    ReadOnlySequence<byte> sliced = readResult.Buffer.Slice( 0, buffer.Length );
                    sliced.CopyTo( buffer.Span );
                    testPipe.Input.AdvanceTo( sliced.End );

                    if( !buffer.Span.SequenceEqual( truthBuffer.Span ) ) Assert.Fail( "Buffer not equals." );
                }
            }
        }
        public static async Task SendToClient( this PacketReplayer @this, IActivityMonitor m, ReadOnlyMemory<byte> data )
        {
            using( m.OpenInfo( "Sending to client..." ) )
            {
                var testPipe = await @this.Channel!.GetTestDuplexPipe();
                await testPipe.Output.WriteAsync( data );
                await testPipe.Output.FlushAsync();
            }
        }

        public static Task SendToClient( this PacketReplayer @this, IActivityMonitor m, string hexArray ) =>
            @this.SendToClient( m, Convert.FromHexString( hexArray ) );

        public static async Task ConnectClient( this PacketReplayer @this, IActivityMonitor m, TestMqttClient client )
        {
            var task = client.ConnectAsync();
            await @this.AssertClientSent( TestHelper.Monitor,
                "101600044d5154540402" + Convert.ToHexString( BitConverter.GetBytes( client.Config.KeepAliveSeconds ).Reverse().ToArray() ) + "000a434b4d71747454657374"
            );
            await @this.SendToClient( TestHelper.Monitor, "20020000" );
            await task;
            await @this.ShouldContainEventAsync<TestMqttClient.Connected>();
        }

        public static async Task<T> ShouldContainEventAsync<T>( this PacketReplayer @this )
        {
            var task = @this.Events.Reader.ReadAsync().AsTask();
            if( !await task.WaitAsync( 60000 ) )
            {
                Assert.Fail( "The replayer didn't had any event." );
            }
            var res = await task;
            if( res is T casted )
            {
                return casted;
            }
            Assert.Fail( $"Expected event of type {typeof( T )} got {res?.GetType()?.ToString() ?? "null"} instead" );
            throw new InvalidOperationException();
        }
    }
}
