using CK.Core;
using CK.MQTT.Client.Processes;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Processes;
using CK.MQTT.Common.Stores;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Sdk
{
    public class MqttClient : IMqttClient
    {
        readonly MqttConfiguration _configuration;
        readonly IPacketStoreManager _storeFactory;
        public MqttClient(
            IPacketStoreManager packetStoreManager,
            MqttConfiguration configuration )
        {
            _storeFactory = packetStoreManager;
            _configuration = configuration;
        }

        IPacketStore? _store;

        /// <summary>
        /// The ClientId of the <see cref="MqttClient"/>. <see cref="null"/> until connected.
        /// </summary>
        public string? ClientId { get; private set; }

        /// <summary>
        /// <see cref="null"/> until connected
        /// </summary>

        #region Events
        readonly SequentialEventHandlerSender<IMqttClient, MqttEndpointDisconnected> _eSeqDisconnect = new SequentialEventHandlerSender<IMqttClient, MqttEndpointDisconnected>();
        public event SequentialEventHandler<IMqttClient, MqttEndpointDisconnected> Disconnected
        {
            add => _eSeqDisconnect.Add( value );
            remove => _eSeqDisconnect.Remove( value );
        }

        readonly SequentialEventHandlerAsyncSender<IMqttClient, MqttEndpointDisconnected> _eSeqDisconnectAsync = new SequentialEventHandlerAsyncSender<IMqttClient, MqttEndpointDisconnected>();
        public event SequentialEventHandlerAsync<IMqttClient, MqttEndpointDisconnected> DisconnectedAsync
        {
            add => _eSeqDisconnectAsync.Add( value );
            remove => _eSeqDisconnectAsync.Remove( value );
        }

        public Task RaiseDisconnectAsync( IActivityMonitor m, MqttEndpointDisconnected disconnect )
        {
            Task task = _eParDisconnectAsync.RaiseAsync( m, this, disconnect );
            _eSeqDisconnect.Raise( m, this, disconnect );
            return Task.WhenAll( task, _eSeqDisconnectAsync.RaiseAsync( m, this, disconnect ) );
        }

        public Task<OutgoingApplicationMessage?> WaitMessageReceivedAsync( Func<OutgoingApplicationMessage, bool>? predicate = null, int timeoutMillisecond = -1 )
            => _eSeqMessage.WaitAsync( predicate, timeoutMillisecond );

        readonly SequentialEventHandlerAsyncSender<IMqttClient, OutgoingApplicationMessage> _eSeqMessageAsync = new SequentialEventHandlerAsyncSender<IMqttClient, OutgoingApplicationMessage>();
        public event SequentialEventHandlerAsync<IMqttClient, OutgoingApplicationMessage> MessageReceivedAsync
        {
            add => _eSeqMessageAsync.Add( value );
            remove => _eSeqMessageAsync.Remove( value );
        }

        /// <summary>
        /// Raise message to events handlers.
        /// </summary>
        /// <param name="m">The monitor.</param>
        /// <param name="message">The message to raise.</param>
        /// <returns></returns>
        Task RaiseMessageAsync( IActivityMonitor m, OutgoingApplicationMessage message )
        {
            Task task = _eParMessageAsync.RaiseAsync( m, this, message );
            _eSeqMessage.Raise( m, this, message );
            return Task.WhenAll( task, _eSeqMessageAsync.RaiseAsync( m, this, message ) );
        }
        #endregion Events

        #region Session Helpers
        async Task CloseClientSessionAsync( IActivityMonitor m )
        {
            if( string.IsNullOrEmpty( ClientId ) ) return;
            if( _store == null ) throw new NullReferenceException( nameof( _store ) );
            await _store.CloseAsync( m );
        }

        async Task OpenClientSession( IActivityMonitor m, bool cleanSession )
        {
            if( ClientId == null ) throw new NullReferenceException( nameof( ClientId ) + " should not be null." );
            _store = await _storeFactory.CreateAsync( m, _configuration.ConnectionString, !cleanSession );
        }
        #endregion Session Helpers

        #region Connection

        bool _isProtocolConnected;
        public async ValueTask<bool> CheckConnectionAsync( IActivityMonitor m )
        {
            if( _isProtocolConnected && !(_channel?.IsConnected ?? false) )
            {
                await CloseAsync( m, DisconnectedReason.RemoteDisconnected );
            }
            return _isProtocolConnected && (_channel?.IsConnected ?? false);
        }


        Task CloseAsync( IActivityMonitor m, Exception e )
        {
            using( m.OpenError( e ) )
            {
                return CloseAsync( m, DisconnectedReason.UnspecifiedError, e.Message );
            }
        }

        async Task CloseAsync( IActivityMonitor m, DisconnectedReason reason, string? message = null )
        {
            using( m.OpenInfo( $"Client {ClientId} - Disconnecting: {reason}" ) )
            {
                var disconnect = new MqttEndpointDisconnected( reason, message );
                using( m.OpenInfo( $"Client {ClientId} - Closing." ) )
                {
                    await CloseClientSessionAsync( m );
                    if( _channel != null )
                    {
                        await _channel.CloseAsync( m, CancellationToken.None );
                        _channel?.Dispose();
                    }
                    _isProtocolConnected = false;
                    ClientId = null;
                }
                await RaiseDisconnectAsync( m, disconnect );
            }
        }

        public Task<ConnectResult> ConnectAnonymousAsync( IActivityMonitor m, LastWill? will = null )
            => ConnectAsync( m, new MqttClientCredentials(), will, cleanSession: true );

        static string GetAnonymousClientId() => "anonymous" + Guid.NewGuid().ToString().Replace( "-", "" ).Substring( 0, 10 );

        public async Task<ConnectResult> ConnectAsync( IActivityMonitor m, MqttClientCredentials credentials, LastWill? will = null, bool cleanSession = false )
        {
            if( await CheckConnectionAsync( m ) ) throw new InvalidOperationException( $"The protocol connection cannot be performed because an active connection for client {ClientId} already exists" );
            ClientId = string.IsNullOrEmpty( credentials.ClientId ) ?
                        GetAnonymousClientId() :
                        credentials.ClientId;
            using( m.OpenInfo( $"Connecting to server with client id '{ClientId}'." ) )
            {
                try
                {
                    await OpenClientSession( m, cleanSession );
                    _channel = await _channelFactory.CreateAsync( m, _configuration.ConnectionString );
                    return await ConnectProcess.ExecuteConnectProtocol(
                        m, _channel, ClientId, cleanSession, MqttProtocol.SupportedLevel,
                        credentials.UserName, credentials.Password, will,
                        _configuration.KeepAliveSecs, _configuration.WaitTimeoutSecs,
                        "MQTT" );//TODO: Const object
                }
                catch( Exception e )
                {
                    await CloseAsync( m, e );
                    throw;
                }
            }
        }

        public async Task DisconnectAsync( IActivityMonitor m )
        {
            using( m.OpenInfo( "Disconnecting..." ) )
            {
                if( !await CheckConnectionAsync( m ) ) return;
                try
                {
                    if( _channel == null )
                    {
                        m.Warn( "Channel was null when disconnecting." );
                        return;
                    }
                    await DisconnectProcess.ExecuteDisconnectProtocol( m, _channel, _configuration.WaitTimeoutSecs );
                }
                finally
                {
                    await CloseAsync( m, DisconnectedReason.SelfDisconnected );
                }
            }
        }

        async ValueTask EnsureConnected( IActivityMonitor m )
        {
            if( !await CheckConnectionAsync( m ) ) throw new InvalidOperationException( "Client is not connected to server." );
            if( _channel == null ) throw new NullReferenceException( $"{nameof( _channel )} is null but we are connected." );
            if( _store == null ) throw new NullReferenceException( $"{nameof( _store )} is null but we are connected." );
        }
        #endregion Connection

        public async ValueTask<ValueTask> PublishAsync( IActivityMonitor m, string topic, ReadOnlyMemory<byte> payload, QualityOfService qos, bool retain = false )
        {
            await EnsureConnected( m );
            return await PublishSenderProcesses.Publish( m, _channel!, _store!, topic, payload, qos, retain, _configuration.WaitTimeoutSecs );
        }

        public async Task<Task<IReadOnlyCollection<SubscribeReturnCode>?>> SubscribeAsync( IActivityMonitor m, params Subscription[] subscriptions )
        {
            await EnsureConnected( m );
            return await SubscribeProcess.ExecuteSubscribeProtocol( m, _channel!, _store!, subscriptions, _configuration.WaitTimeoutSecs * 1000 );
        }

        public async Task<Task<bool>> UnsubscribeAsync( IActivityMonitor m, IEnumerable<string> topics )
        {
            await EnsureConnected( m );
            return await UnsubscribeProcess.ExecuteUnsubscribeProtocol( m, _channel!, _store!, topics, _configuration.WaitTimeoutSecs * 1000 );
        }
    }
}
