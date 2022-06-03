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

namespace CK.MQTT.Client
{
    public class MessageExchangerAgent<T> : MessageExchangerAgentBase<T> where T : IConnectedMessageExchanger
    {
        readonly PerfectEventSender<ApplicationMessage> _onMessageSender = new();
        readonly PerfectEventSender<VolatileApplicationMessage> _onVolatileMessageSender = new();
        readonly PerfectEventSender<RefCountingApplicationMessage> _refcountingMessageSender = new();
        readonly PerfectEventSender<DisconnectReason> _onConnectionChangeSender = new();
        readonly PerfectEventSender<ushort> _onStoreQueueFilling = new();

        public ref struct EventTypeChoice
        {
            public PerfectEvent<ApplicationMessage> Simple { get; }
            public PerfectEvent<VolatileApplicationMessage> Rented { get; }
            public PerfectEvent<RefCountingApplicationMessage> RefCounted { get; }

            public EventTypeChoice(
                PerfectEvent<ApplicationMessage> simple,
                PerfectEvent<VolatileApplicationMessage> rented,
                PerfectEvent<RefCountingApplicationMessage> refcounted
                )
            {
                Simple = simple;
                Rented = rented;
                RefCounted = refcounted;
            }
        }

        public EventTypeChoice OnMessage => new(
            _onMessageSender.PerfectEvent,
            _onVolatileMessageSender.PerfectEvent,
            _refcountingMessageSender.PerfectEvent
        );

        public PerfectEvent<DisconnectReason> OnConnectionChange => _onConnectionChangeSender.PerfectEvent;

        public PerfectEvent<ushort> OnStoreQueueFilling => _onStoreQueueFilling.PerfectEvent;

        protected Task? WorkLoop { get; set; }
        public MessageExchangerAgent( Func<IMqtt3Sink, T> clientFactory ) : base( clientFactory )
        {
        }

        [MemberNotNull( nameof( WorkLoop ) )]
        public override void Start()
        {
            if( WorkLoop == null )
            {
                base.Start();
                WorkLoop = WorkLoopAsync( Events.Reader );
            }
            Debug.Assert( Events != null );
        }

        protected override IMqtt3Sink.ManualConnectRetryBehavior OnFailedManualConnect( ConnectResult connectResult )
            => throw new NotSupportedException( "https://github.com/signature-opensource/CK-MQTT/issues/35" );

        public override Task<bool> DisconnectAsync( bool deleteSession )
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
            var res = await base.DisconnectAsync( deleteSession );
            await StopAsync( waitForCompletion );
            return res;
        }


        protected override async ValueTask StopAsync( bool waitForCompletion )
        {
            await base.StopAsync( waitForCompletion );
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

        virtual protected async Task WorkLoopAsync( ChannelReader<object?> channel )
        {
            ActivityMonitor m = new();
            await foreach( var item in channel.ReadAllAsync() )
            {
                if( item is VolatileApplicationMessage msg )
                {
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
                            var taskB = _onVolatileMessageSender.HasHandlers ? _onVolatileMessageSender.RaiseAsync( m, new VolatileApplicationMessage( appMessage, new DisposableComposite() ) ) : Task.CompletedTask;
                            var taskC = _refcountingMessageSender.HasHandlers ? _refcountingMessageSender.RaiseAsync( m, new RefCountingApplicationMessage( appMessage, new DisposableComposite() ) ) : Task.CompletedTask;
                            await Task.WhenAll( taskA, taskB, taskC );
                            return;
                        }
                        // here there is no "_onMessageSender"
                        // no allocation is needed then.
                        bool hasRefCount = _refcountingMessageSender.HasHandlers;
                        bool hasVolatile = _onVolatileMessageSender.HasHandlers;

                        if( hasRefCount && hasVolatile )
                        {
                            // this make the volatile handler an user of the refcounting, avoid the allocation
                            var appMessage = new RefCountingApplicationMessage( msg.Message, msg );
                            appMessage.IncrementRef();
                            var taskC = _refcountingMessageSender.RaiseAsync( m, appMessage );
                            var taskB = _onVolatileMessageSender.RaiseAsync( m, new VolatileApplicationMessage( appMessage.ApplicationMessage, new RefCountingWrapper( appMessage ) ) );
                            await Task.WhenAll( taskB, taskC );
                        }
                        if( hasRefCount )
                        {
                            var appMessage = new RefCountingApplicationMessage( msg.Message, msg );
                            await _refcountingMessageSender.RaiseAsync( m, appMessage );
                        }
                        if( hasVolatile )
                        {
                            await _onVolatileMessageSender.RaiseAsync( m, msg );
                        }
                    }

                }
                else if( item is UnattendedDisconnect disconnect )
                {
                    await _onConnectionChangeSender.RaiseAsync( m, disconnect.Reason );
                }
                else if( item is Connected )
                {
                    await _onConnectionChangeSender.RaiseAsync( m, DisconnectReason.None );
                }
                else if( item is StoreFilling storeFilling )
                {
                    await _onStoreQueueFilling.SafeRaiseAsync( m, storeFilling.FreeLeftSlot );
                }
                else if( item is UnparsedExtraData extraData )
                {
                    m.Warn( $"There was {extraData.UnparsedData.Length} bytes unparsed in Packet {extraData.PacketId}." );
                }
                else if( item is ReconnectionFailed )
                {
                    m.Warn( $"Reconnection failed." );
                }
                else if( item is QueueFullPacketDestroyed queueFullPacketDestroyed )
                {
                    m.Warn( $"Because the queue is full, {queueFullPacketDestroyed.PacketType} with id {queueFullPacketDestroyed.PacketId} has been dropped. " );
                }
                else if( item is TaskCompletionSource tcs )
                {
                    tcs.SetResult();
                }
                else
                {
                    m.Error( "Unknown event has been sent on the channel." );
                }
            }
        }
    }
}
