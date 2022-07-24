using CK.Core;
using CK.MQTT.Client.ExtensionMethods;
using CK.MQTT.Packets;
using CK.PerfectEvent;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CK.MQTT.Client.MQTTMessageSink;

namespace CK.MQTT.Client
{
    public abstract class MessageExchangerAgent : IConnectedMessageSender
    {
        readonly PerfectEventSender<ApplicationMessage> _onMessageSender = new();
        readonly PerfectEventSender<RefCountingApplicationMessage> _refcountingMessageSender = new();
        protected readonly PerfectEventSender<DisconnectReason> _onConnectionChangeSender = new();
        readonly PerfectEventSender<ushort> _onStoreQueueFilling = new();
        private Channel<object?>? _events;

        public ref struct EventTypeChoice
        {
            public PerfectEvent<ApplicationMessage> Simple { get; }
            public PerfectEvent<RefCountingApplicationMessage> RefCounted { get; }

            public EventTypeChoice(
                PerfectEvent<ApplicationMessage> simple,
                PerfectEvent<RefCountingApplicationMessage> refcounted
                )
            {
                Simple = simple;
                RefCounted = refcounted;
            }
        }

        public EventTypeChoice OnMessage => new(
            _onMessageSender.PerfectEvent,
            _refcountingMessageSender.PerfectEvent
        );


        protected abstract MQTTMessageSink MessageSink { get; }
        public IConnectedMessageSender Sender => MessageSink.Sender;

        public PerfectEvent<DisconnectReason> OnConnectionChange => _onConnectionChangeSender.PerfectEvent;

        public PerfectEvent<ushort> OnStoreQueueFilling => _onStoreQueueFilling.PerfectEvent;

        protected Channel<object?>? Events
        {
            get => _events;
            private set
            {
                MessageSink.Events = value?.Writer!; // We set the value to null, the sink should not use the events in this meantime !
                // We expect the client to be in a "dead" state where no message will be emitted.
                _events = value;
            }
        }
        protected Task? WorkLoop { get; set; }

        [MemberNotNull( nameof( WorkLoop ) )]
        public void Start()
        {
            if( Events != null ) throw new InvalidOperationException( "Already started." );
            Events = Channel.CreateUnbounded<object?>( new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = false
            } );
            WorkLoop = WorkLoopAsync( Events.Reader );
        }

        public Task<bool> DisconnectAsync( bool deleteSession )
            => DisconnectAsync( deleteSession, true );


        /// <param name="deleteSession"></param>
        /// <param name="waitForCompletion">Wait the pump messages to empty all the messages.</param>
        /// <returns></returns>
        public async Task<bool> DisconnectAsync( bool deleteSession, bool waitForCompletion )
        {
            // 2 scenario:
            // 1. agent doesn't support reconnecting, it can be disconnected only once, and it called Start in base ctor.
            // 2. agent support reconnecting, in this case it will call Start() at each connection.
            // in these 2 scenario, Messages & WorkLoop is not null because set in base constructor.
            var res = await Sender.DisconnectAsync( deleteSession );
            await StopAsync( waitForCompletion );
            return res;
        }


        protected async ValueTask StopAsync( bool waitForCompletion )
        {
            Events!.Writer.Complete();
            Events = null;
            if( waitForCompletion )
            {
                await WorkLoop!;
            }
            WorkLoop = null;
        }

        class RefCountingWrapper : IDisposable
        {
            readonly RefCountingApplicationMessage _msg;

            public RefCountingWrapper( RefCountingApplicationMessage msg )
            {
                _msg = msg;
            }

            public void Dispose()
            {
                _msg.DecrementRef();
            }
        }

        DisconnectReason _reason;
        virtual protected async Task WorkLoopAsync( ChannelReader<object?> channel )
        {
            ActivityMonitor m = new();
            await foreach( var item in channel.ReadAllAsync() )
            {
                await ProcessMessageAsync( m, item );
            }
        }

        protected virtual async Task ProcessMessageAsync( IActivityMonitor m, object? item )
        {
            switch( item )
            {
                case VolatileApplicationMessage msg:
                    using( m.OpenTrace( $"Incoming MQTT Application Message '{msg.Message}'..." ) )
                    {
                        // all this part may be subject to various race condition.
                        // this is not very important, there is 
                        var mustAllocate = _onMessageSender.HasHandlers;
                        if( mustAllocate )
                        {
                            var buffer = msg.Message.Payload.ToArray();
                            var appMessage = new ApplicationMessage( msg.Message.Topic, buffer, msg.Message.QoS, msg.Message.Retain );
                            msg.Dispose();
                            var taskA = _onMessageSender.SafeRaiseAsync( m, appMessage );
                            var refCounting = new RefCountingApplicationMessage( appMessage, new DisposableComposite() );
                            refCounting.IncrementRef();
                            var taskC = _refcountingMessageSender.HasHandlers ? _refcountingMessageSender.RaiseAsync( m, refCounting ) : Task.CompletedTask;
                            await Task.WhenAll( taskA, taskC );
                            refCounting.DecrementRef();
                            return;
                        }
                        // here there is no "_onMessageSender"
                        // no allocation is needed then.
                        if( _refcountingMessageSender.HasHandlers )
                        {
                            var appMessage = new RefCountingApplicationMessage( msg.Message, msg );
                            appMessage.IncrementRef();
                            await _refcountingMessageSender.RaiseAsync( m, appMessage );
                            appMessage.DecrementRef();
                        }
                    }
                    return;
                case UnattendedDisconnect disconnect:
                    _reason = disconnect.Reason;
                    await _onConnectionChangeSender.RaiseAsync( m, disconnect.Reason );
                    return;
                case StoreFilling storeFilling:
                    await _onStoreQueueFilling.SafeRaiseAsync( m, storeFilling.FreeLeftSlot );
                    return;
                case RaiseConnectionState connectionState:
                    await _onConnectionChangeSender.RaiseAsync( m, _reason );
                    return;
                case UnparsedExtraData extraData:
                    m.Warn( $"There was {extraData.UnparsedData.Length} bytes unparsed in Packet {extraData.PacketId}." );
                    return;
                case QueueFullPacketDestroyed queueFullPacketDestroyed:
                    m.Warn( $"Because the queue is full, {queueFullPacketDestroyed.PacketType} with id {queueFullPacketDestroyed.PacketId} has been dropped. " );
                    return;
                case TaskCompletionSource tcs:
                    tcs.SetResult();
                    return;
                default:
                    m.Error( "Unknown event has been sent on the channel." );
                    return;
            }
        }
        public string? ClientId => Sender.ClientId;


        public ValueTask<Task> PublishAsync( OutgoingMessage message ) => Sender.PublishAsync( message );

        public ValueTask DisposeAsync() => Sender.DisposeAsync();
        public record RaiseConnectionState();
        public void RaiseOnConnectionChangeWithLatestState()
        {
            var events = Events;
            if( events == null ) Throw.InvalidOperationException( "Not started." );
            events.Writer.TryWrite( new RaiseConnectionState() );
        }
    }
}
