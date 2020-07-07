using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT
{
    class TestImpl : IMqttClient
    {
        readonly SequentialEventHandlerSender<IMqttClient, OutgoingApplicationMessage> _eSeqMessage;
        readonly SequentialEventHandlerAsyncSender<IMqttClient, OutgoingApplicationMessage> _eSeqMessageAsync;
        readonly SequentialEventHandlerSender<IMqttClient, MqttEndpointDisconnected> _eSeqDisconnect;
        readonly SequentialEventHandlerAsyncSender<IMqttClient, MqttEndpointDisconnected> _eSeqDisconnectAsync;

        public TestImpl( IMqttLogger monitor )
        {
            _eSeqMessage = new SequentialEventHandlerSender<IMqttClient, OutgoingApplicationMessage>();
            _eSeqMessageAsync = new SequentialEventHandlerAsyncSender<IMqttClient, OutgoingApplicationMessage>();

            _eSeqDisconnect = new SequentialEventHandlerSender<IMqttClient, MqttEndpointDisconnected>();
            _eSeqDisconnectAsync = new SequentialEventHandlerAsyncSender<IMqttClient, MqttEndpointDisconnected>();

            Monitor = monitor;
        }
        public IMqttLogger Monitor { get; }

        /// <summary>
        /// This is THE sender: first, async parallels are launched, then the synchronous ones and then sequential async ones
        /// and we wait for the parallels to complete...
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="payload"></param>
        /// <returns></returns>
        public Task DoReceiveMessageAsync( string topic, byte[] payload )
        {
            var msg = new SmallOutgoingApplicationMessage( false, false, topic, QualityOfService.AtLeastOnce, payload );
            _eSeqMessage.Raise( Monitor, this, msg );
            return _eSeqMessageAsync.RaiseAsync( Monitor, this, msg );
        }

        public async Task DoSafeReceiveMessageAsync( string topic, byte[] payload )
        {
            try
            {
                var msg = new SmallOutgoingApplicationMessage( false, false, topic, QualityOfService.AtLeastOnce, payload );
                _eSeqMessage.Raise( Monitor, this, msg );
                await _eSeqMessageAsync.RaiseAsync( Monitor, this, msg );
            }
            catch( Exception ex )
            {
                Monitor.Error( ex );
            }
        }

        public event SequentialEventHandler<IMqttClient, OutgoingApplicationMessage> MessageReceived
        {
            add { _eSeqMessage.Add( value ); }
            remove { _eSeqMessage.Remove( value ); }
        }

        public event SequentialEventHandlerAsync<IMqttClient, OutgoingApplicationMessage> MessageReceivedAsync
        {
            add { _eSeqMessageAsync.Add( value ); }
            remove { _eSeqMessageAsync.Remove( value ); }
        }

        public event SequentialEventHandler<IMqttClient, MqttEndpointDisconnected> Disconnected
        {
            add { _eSeqDisconnect.Add( value ); }
            remove { _eSeqDisconnect.Remove( value ); }
        }

        public event SequentialEventHandlerAsync<IMqttClient, MqttEndpointDisconnected> DisconnectedAsync
        {
            add { _eSeqDisconnectAsync.Add( value ); }
            remove { _eSeqDisconnectAsync.Remove( value ); }
        }

        event SequentialEventHandlerAsync<IMqttClient, IncomingApplicationMessage> IMqttClient.MessageReceivedAsync
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        public string ClientId => throw new NotImplementedException();

        public bool IsConnected => throw new NotImplementedException();

        public Task DisconnectAsync( IMqttLogger m )
        {
            throw new NotImplementedException();
        }

        public Task<OutgoingApplicationMessage> WaitMessageReceivedAsync( Func<OutgoingApplicationMessage, bool> predicate = null, int timeoutMillisecond = -1 )
        {
            throw new NotImplementedException();
        }

        public Task<Task<IReadOnlyCollection<SubscribeReturnCode>>> SubscribeAsync( IMqttLogger m, params Subscription[] subscriptions )
        {
            throw new NotImplementedException();
        }

        public ValueTask<ValueTask> PublishAsync( IMqttLogger m, string topic, ReadOnlyMemory<byte> payload, QualityOfService qos, bool retain = false )
        {
            throw new NotImplementedException();
        }

        public Task<IncomingApplicationMessage> WaitMessageReceivedAsync( Func<IncomingApplicationMessage, bool> predicate = null, int timeoutMillisecond = -1 )
        {
            throw new NotImplementedException();
        }

        ValueTask<Task<SubscribeReturnCode[]>> IMqttClient.SubscribeAsync( IMqttLogger m, params Subscription[] subscriptions )
        {
            throw new NotImplementedException();
        }

        public ValueTask<Task> PublishAsync( IMqttLogger m, OutgoingApplicationMessage message )
        {
            throw new NotImplementedException();
        }

        public ValueTask<Task> UnsubscribeAsync( IMqttLogger m, params string[] topics )
        {
            throw new NotImplementedException();
        }

        ValueTask IMqttClient.DisconnectAsync( IMqttLogger m )
        {
            throw new NotImplementedException();
        }

        public ValueTask<Task<ConnectResult>> ConnectAsync( IMqttLogger m, MqttClientCredentials credentials = null, OutgoingLastWill lastWill = null )
        {
            throw new NotImplementedException();
        }
    }


    public class Tests
    {
        class MqttCientConsumer
        {
            readonly IMqttLogger _monitor;
            readonly string _name;
            readonly Random _rand;

            public MqttCientConsumer( string name )
            {
                _name = name;
                _rand = new Random( name.GetHashCode() );
            }

            public string LastTopic { get; private set; }
        }

        [Test]
        public async Task event_async_and_sync_look_the_same()
        {
            var impl = new TestImpl( new MqttActivityMonitor( TestHelper.Monitor ));

            var consumer1 = new MqttCientConsumer( "Consumer1" );
            var consumer2 = new MqttCientConsumer( "Consumer2" );
            var consumer3 = new MqttCientConsumer( "Consumer3" );

            impl.MessageReceived += SyncReceiving;
            impl.MessageReceivedAsync += SequentialAsyncReceiving;

            await impl.DoReceiveMessageAsync( "topic", Array.Empty<byte>() );
            LastTopicSync.Should().Be( "topic" );
            LastTopicASync.Should().Be( "topic" );
            consumer1.LastTopic.Should().Be( "topic" );
            consumer2.LastTopic.Should().Be( "topic" );
            consumer3.LastTopic.Should().Be( "topic" );

            await impl.DoReceiveMessageAsync( "topic2", Array.Empty<byte>() );
            LastTopicSync.Should().Be( "topic2" );
            LastTopicASync.Should().Be( "topic2" );
            consumer1.LastTopic.Should().Be( "topic2" );
            consumer2.LastTopic.Should().Be( "topic2" );
            consumer3.LastTopic.Should().Be( "topic2" );

            impl.MessageReceivedAsync -= SequentialAsyncReceiving;
            await impl.DoReceiveMessageAsync( "topic3", Array.Empty<byte>() );
            LastTopicSync.Should().Be( "topic3" );
            LastTopicASync.Should().Be( "topic2" );
            consumer1.LastTopic.Should().Be( "topic3" );
            consumer2.LastTopic.Should().Be( "topic3" );
            consumer3.LastTopic.Should().Be( "topic3" );

            impl.MessageReceivedAsync += SequentialAsyncReceiving;
            await impl.DoReceiveMessageAsync( "topic4", Array.Empty<byte>() );
            LastTopicSync.Should().Be( "topic4" );
            LastTopicASync.Should().Be( "topic4" );
            consumer1.LastTopic.Should().Be( "topic4" );
            consumer2.LastTopic.Should().Be( "topic4" );
            consumer3.LastTopic.Should().Be( "topic4" );
        }

        static string LastTopicSync;
        static string LastTopicASync;

        private static void SyncReceiving( IMqttLogger monitor, IMqttClient sender, OutgoingApplicationMessage e )
        {
            LastTopicSync = e.Topic;
            monitor.Info( "Synchronous Reception." );
        }

        static Task SequentialAsyncReceiving( IMqttLogger monitor, IMqttClient sender, OutgoingApplicationMessage e )
        {
            LastTopicASync = e.Topic;
            monitor.Info( "Asynchronous Reception (but sequential)." );
            return Task.CompletedTask;
        }
    }
}
