using CK.Core;
using CK.MQTT.Client.ExtensionMethods;
using CK.PerfectEvent;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Middleware
{
    class HandlePublish : IAgentMessageMiddleware
    {
        readonly PerfectEventSender<ApplicationMessage> _onMessageSender;
        readonly PerfectEventSender<RefCountingApplicationMessage> _refcountingMessageSender;

        public HandlePublish( PerfectEventSender<ApplicationMessage> onMessageSender, PerfectEventSender<RefCountingApplicationMessage> refcountingMessageSender )
        {
            _onMessageSender = onMessageSender;
            _refcountingMessageSender = refcountingMessageSender;
        }

        public ValueTask DisposeAsync() => new ValueTask();

        public async ValueTask<bool> HandleAsync( IActivityMonitor m, object? message )
        {
            if( message is not VolatileApplicationMessage msg ) return false;

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
                }
                else if( _refcountingMessageSender.HasHandlers )
                {
                    // here there is no "_onMessageSender"
                    // no allocation is needed then.
                    var appMessage = new RefCountingApplicationMessage( msg.Message, msg );
                    appMessage.IncrementRef();
                    await _refcountingMessageSender.RaiseAsync( m, appMessage );
                    appMessage.DecrementRef();
                }
                return true;
            }
        }
    }
}
