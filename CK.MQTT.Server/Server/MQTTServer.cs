using CK.Core;
using CK.MQTT.Client.ExtensionMethods;
using CK.MQTT.Packets;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Server;

public class MQTTServer : MQTTDemiServer
{
    public MQTTServer( MQTT3ConfigurationBase config, IMQTTChannelFactory channelFactory, IStoreFactory storeFactory, IAuthenticationProtocolHandlerFactory authenticationProtocolHandler ) : base( config, channelFactory, storeFactory, authenticationProtocolHandler )
    {
        OnNewClient.Sync += OnNewClient_Sync;
    }

    private void OnNewClient_Sync( IActivityMonitor monitor, MQTTServerAgent e )
    {
        var state = new ClientState( this, e );
        e.OnMessage.RefCounted.Async += state.ListenMessageAsync;
        e.OnConnectionChange.Sync += state.OnConnectionChange;
        _agents.Add( state );
    }

    class ClientState
    {
        readonly MQTTServer _parent;
        readonly SimpleTopicManager _topicManager = new();
        readonly MQTTServerAgent _agent;
        readonly ReaderWriterLockSlim _topicManagerLock = new();

        public ClientState( MQTTServer parent, MQTTServerAgent agent )
        {
            _parent = parent;
            _agent = agent;
            _agent.OnSubscribe.Sync += OnSubscribe_Sync;
            _agent.OnUnsubscribe.Sync += OnUnsubscribe_Sync;
        }

        private void OnUnsubscribe_Sync( IActivityMonitor monitor, string e )
        {
            _topicManagerLock.EnterWriteLock();
            _topicManager.Unsubscribe( e );
            _topicManagerLock.ExitWriteLock();
        }

        private void OnSubscribe_Sync( IActivityMonitor monitor, Subscription e )
        {
            _topicManagerLock.EnterWriteLock();
            _topicManager.Subscribe( e.TopicFilter );
            _topicManagerLock.ExitWriteLock();
        }

        public void OnConnectionChange( IActivityMonitor m, DisconnectReason reason )
        {
            if( reason != DisconnectReason.None )
            {
                _parent._agentLock.EnterWriteLock();
                _parent._agents.Remove( this );
                _parent._agentLock.ExitWriteLock();
            }
        }

        public class RefCountedWrapperAppMessage : OutgoingMessage
        {
            readonly RefCountingApplicationMessage _msg;

            public RefCountedWrapperAppMessage( RefCountingApplicationMessage msg )
                : base( msg.ApplicationMessage.Topic, QualityOfService.AtMostOnce, msg.ApplicationMessage.Retain )
            {
                _msg = msg;
                msg.IncrementRef();
            }

            protected override uint PayloadSize => (uint)_msg.ApplicationMessage.Payload.Length;

            public override ValueTask DisposeAsync()
            {
                _msg.DecrementRef();
                return new();
            }

            protected override async ValueTask WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
            {
                await pw.WriteAsync( _msg.ApplicationMessage.Payload, cancellationToken );
            }
        }

        public async Task ListenMessageAsync( IActivityMonitor m, RefCountingApplicationMessage msg, CancellationToken cancellationToken )
        {
            _parent._agentLock.EnterReadLock();

            foreach( var agentData in _parent._agents )
            {
                agentData._topicManagerLock.EnterReadLock();
                if( !agentData._topicManager.IsFiltered( msg.ApplicationMessage.Topic ) )
                {
                    _ = await agentData._agent.PublishAsync( new RefCountedWrapperAppMessage( msg ) );
                }
                agentData._topicManagerLock.ExitReadLock();
            }

            _parent._agentLock.ExitReadLock();
        }
    }
    readonly ReaderWriterLockSlim _agentLock = new();
    readonly List<ClientState> _agents = new();


}
