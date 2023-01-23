using CK.Core;
using CK.MQTT.Client.ExtensionMethods;
using CK.MQTT.Client.Middleware;
using CK.MQTT.Packets;
using CK.PerfectEvent;
using System.Threading.Tasks;
namespace CK.MQTT.Client
{

    public class MessageExchangerAgent : IConnectedMessageSender
    {

        readonly PerfectEventSender<ApplicationMessage> _onMessageSender = new();
        readonly PerfectEventSender<RefCountingApplicationMessage> _refCountingMessageSender = new();
        protected readonly PerfectEventSender<DisconnectReason> _onConnectionChangeSender = new();
        readonly PerfectEventSender<ushort> _onStoreQueueFilling = new();
        readonly MessageWorker _messageWorker;
        readonly IConnectedMessageSender _sender;

        public MessageExchangerAgent( IConnectedMessageSender connectedMessageSender, MessageWorker messageWorker )
        {
            _messageWorker = messageWorker;
            messageWorker.Middlewares.AddRange(
                new IAgentMessageMiddleware[]
                {
                    new HandleDisconnect( _onConnectionChangeSender ),
                    new HandlePublish( _onMessageSender, _refCountingMessageSender ),
                    new MessageExchangerLoggingMiddleware(),
                    new HandleSynchronize()
                }
            );
            _sender = connectedMessageSender;
        }

    public readonly ref struct EventTypeChoice
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
        _refCountingMessageSender.PerfectEvent
    );

    public PerfectEvent<DisconnectReason> OnConnectionChange => _onConnectionChangeSender.PerfectEvent;

    public PerfectEvent<ushort> OnStoreQueueFilling => _onStoreQueueFilling.PerfectEvent;

    public Task<bool> DisconnectAsync( bool deleteSession ) => _sender.DisconnectAsync( deleteSession );

    /// <summary>
    /// Send a message in the event queue and wait for it to be processed.
    /// </summary>
    /// <returns></returns>
    public async Task SynchronizeEventLoopAsync()
    {
        var tcs = new TaskCompletionSource();
        _messageWorker.QueueMessage( new SynchronizationMessage( tcs ) );
        await tcs.Task;
    }

    public ValueTask<Task> PublishAsync( OutgoingMessage message ) => _sender.PublishAsync( message );

    public async ValueTask DisposeAsync()
    {
        var task = _sender.DisposeAsync();
        await _messageWorker.DisposeAsync();
        await task;
    }
}
}
