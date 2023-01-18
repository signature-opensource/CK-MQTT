using CK.Core;
using CK.MQTT.Client.Middleware;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;
namespace CK.MQTT.Client.Tests.Helpers
{
    static class PacketReplayerAssertsExtensions
    {
        public static MQTTClientAgent CreateMQTT3Client( this PacketReplayer replayer, MQTT3ClientConfiguration config, bool withReconnect = false )
        {
            var messageWorker = new MessageWorker();
            messageWorker.Middlewares.Add( new TesterMiddleware( replayer.Events.Writer ) );
            var sink = new DefaultClientMessageSink( messageWorker.MessageWriter );
            var client = new LowLevelMQTTClient( ProtocolConfiguration.MQTT3, config, sink, replayer.CreateChannel() );
            if( withReconnect ) messageWorker.Middlewares.Add( new HandleAutoReconnect( config.TimeUtilities, client, messageWorker.MessageWriter, _ =>
            {
                return TimeSpan.FromSeconds( 5 );
            } ));
            replayer.Client = new MQTTClientAgent( client, messageWorker );
            replayer.Config = config;
            return replayer.Client;
        }

        public static async Task AssertClientSentAsync( this PacketReplayer @this, IActivityMonitor m, string hexArray )
        {
            using( m.OpenInfo( "Outgoing packet..." ) )
            {
                ReadOnlyMemory<byte> truthBuffer = Convert.FromHexString( hexArray );
                Memory<byte> buffer = new byte[truthBuffer.Length];

                using( CancellationTokenSource cts = Debugger.IsAttached ? new() : new( 500 ) )
                {
                    var testPipe = await @this.Channel!.GetTestDuplexPipeAsync();
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
        public static async Task SendToClientAsync( this PacketReplayer @this, IActivityMonitor m, ReadOnlyMemory<byte> data )
        {
            using( m.OpenInfo( "Sending to client..." ) )
            {
                var testPipe = await @this.Channel!.GetTestDuplexPipeAsync();
                await testPipe.Output.WriteAsync( data );
                await testPipe.Output.FlushAsync();
            }
        }

        public static Task SendToClientAsync( this PacketReplayer @this, IActivityMonitor m, string hexArray ) =>
            @this.SendToClientAsync( m, Convert.FromHexString( hexArray ) );

        public static async Task ConnectClientAsync( this PacketReplayer @this, IActivityMonitor m, MQTTClientAgent client )
        {
            var task = client.ConnectAsync( true );
            await @this.AssertClientSentAsync( TestHelper.Monitor,
                "101600044d5154540402" + Convert.ToHexString( BitConverter.GetBytes( @this.Config.KeepAliveSeconds ).Reverse().ToArray() ) + "000a434b4d71747454657374"
            );
            await @this.SendToClientAsync( TestHelper.Monitor, "20020000" );
            await task;
            await @this.ShouldContainEventAsync<LoopBackBase.StartedChannel>();
            await @this.ShouldContainEventAsync<DefaultClientMessageSink.Connected>();
        }

        public static Task<T> ShouldContainEventAsync<T>( this PacketReplayer @this )
            => @this.Events.Reader!.ShouldContainEventAsync<T>();

        public static async Task<T> ShouldContainEventAsync<T>( this ChannelReader<object> @this )
        {
            var task = @this.ReadAsync().AsTask();
            if( !await task.WaitAsync( 60000 ) ) Assert.Fail( "The replayer didn't had any event." );
            var res = await task;
            if( res is not T casted )
            {
                Assert.Fail( $"Expected event of type {typeof( T )} got {res?.GetType()?.ToString() ?? "null"} instead" );
                throw new InvalidOperationException();
            }
            return casted;
        }


        public static async Task<(T1, T2)> ShouldContainEventsAsync<T1, T2>( this PacketReplayer @this )
        {
            var task1 = @this.Events.Reader.ReadAsync().AsTask();
            if( !await task1.WaitAsync( 60000 ) )
            {
                Assert.Fail( "The replayer didn't had any event." );
            }

            var task2 = @this.Events.Reader.ReadAsync().AsTask();
            if( !await task2.WaitAsync( 60000 ) )
            {
                Assert.Fail( "The replayer didn't had any event." );
            }

            var res1 = await task1;
            var res2 = await task2;
            T1 left;
            T2 right;
            if( res1 is T1 casted )
            {
                left = casted;
                right = (T2)res2!;
            }
            else
            {
                left = (T1)res2!;
                right = (T2)res1!;
            }
            return (left, right);
        }
    }
}
