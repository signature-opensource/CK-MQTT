using CK.MQTT.Client;
using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT;

public abstract class MessageExchanger : IConnectedMessageSender
{
    /// <summary>
    /// Instantiate the <see cref="MessageExchanger"/> with the given configuration.
    /// </summary>
    /// <param name="config">The configuration to use.</param>
    /// <param name="messageHandler">The delegate that will handle incoming messages. <see cref="MessageHandlerDelegate"/> docs for more info.</param>
    public MessageExchanger( ProtocolConfiguration pConfig, MQTT3ConfigurationBase config, IMQTT3Sink sink, IMQTTChannel channel, IRemotePacketStore? remotePacketStore = null, ILocalPacketStore? localPacketStore = null )
    {
        _stopTokenSource = new(); // Never null.
        _stopTokenSource.Cancel(); // But default state is stopped. 
        _closeTokenSource = new(); // Never null.
        _closeTokenSource.Cancel();
        PConfig = pConfig;
        Config = config;
        Sink = sink;
        sink.Sender = this;
        Channel = channel;
        RemotePacketStore = remotePacketStore ?? new MemoryPacketIdStore();
        LocalPacketStore = localPacketStore ?? new MemoryPacketStore( pConfig, Config, ushort.MaxValue );
    }

    CancellationTokenSource _stopTokenSource;
    public CancellationToken StopToken => _stopTokenSource.Token;
    CancellationTokenSource _closeTokenSource;
    public CancellationToken CloseToken => _closeTokenSource.Token;

    protected void RenewTokens()
    {
        _stopTokenSource.Dispose();
        // http://web.archive.org/web/20160203062224/http://blogs.msdn.com/b/pfxteam/archive/2012/03/25/10287435.aspx
        // Disposing the CTS is not necessary and would involve locking, etc.
        _closeTokenSource = new CancellationTokenSource();
        _stopTokenSource = CancellationTokenSource.CreateLinkedTokenSource( _closeTokenSource.Token );
    }

    /// <inheritdoc/>
    public MQTT3ConfigurationBase Config { get; }
    public virtual IMQTT3Sink Sink { get; }
    public IRemotePacketStore RemotePacketStore { get; }
    public ILocalPacketStore LocalPacketStore { get; }
    public IMQTTChannel Channel { get; }
    public ProtocolConfiguration PConfig { get; }
    public OutputPump? OutputPump { get; protected set; }
    public InputPump? InputPump { get; protected set; }
    public bool IsConnected => !_stopTokenSource.IsCancellationRequested;

    protected ValueTask<Task<T?>> SendPacketWithQoSAsync<T>( IOutgoingPacket outgoingPacket )
        => outgoingPacket.Qos switch
        {
            QualityOfService.AtLeastOnce => StoreAndSendAsync<T>( outgoingPacket ),
            QualityOfService.ExactlyOnce => StoreAndSendAsync<T>( outgoingPacket ),
            _ => throw new ArgumentException( "Invalid QoS." ),
        };

    async ValueTask<Task<T?>> StoreAndSendAsync<T>( IOutgoingPacket msg )
    {
        (Task<object?> ackReceived, IOutgoingPacket newPacket) = await LocalPacketStore.StoreMessageAsync( msg, msg.Qos );
        return SendAsync<T>( newPacket, ackReceived );
    }

    async Task<T?> SendAsync<T>( IOutgoingPacket packet, Task<object?> ackReceived )
    {
        OutputPump?.TryQueueMessage( packet );
        object? res = await ackReceived;
        if( res is null ) return default;
        if( res is T a ) return a;

        // For example: it will throw if the client send a Publish, and the server answer a SubscribeAck with the same packet id as the publish.
        throw new ProtocolViolationException( $"Expected to find a {typeof( T )} in the store for packet ID {packet.PacketId}, but got {res.GetType()}. This is an implementation bug from the server, or client, or the network didn't respected it's guarantees." );
    }

    public ValueTask<Task> PublishAsync( OutgoingMessage message )
    {
        if( message.Qos == QualityOfService.AtMostOnce ) return PublishQoS0Async( message );
        var vtask = SendPacketWithQoSAsync<object?>( message );
        return UnwrapCastAsync( message, vtask );
    }

    static async ValueTask<Task> UnwrapCastAsync<T>( OutgoingMessage message, ValueTask<Task<T>> vtask )
    {
        var task = await vtask;
        await message.DisposeAsync();
        return task;
    }

    async ValueTask<Task> PublishQoS0Async( OutgoingMessage packet )
    {
        var pump = OutputPump;
        if( pump != null )
        {
            await pump.QueueMessageAsync( packet );
        }
        else
        {
            await packet.DisposeAsync();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the external world to explicitly close the connection to the remote.
    /// </summary>
    /// <returns>True if this call actually closed the connection, false if the connection has already been closed by a concurrent decision.</returns>
    public async Task<bool> DisconnectAsync( bool clearSession )
    {
        Sink.OnUserDisconnect( clearSession );
        return await DoDisconnectAsync( clearSession, DisconnectReason.UserDisconnected );
    }

    protected async ValueTask<bool> CloseAsync( Func<ValueTask> beforeDisconnect, DisconnectReason disconnectReason )
    {
        lock( _stopTokenSource )
        {
            if( _stopTokenSource.IsCancellationRequested ) return false;
            await _stopTokenSource.CancelAsync();
        }
        LocalPacketStore.CancelAllAckTask(); //Cancel acks when we know no more work will come.
        await beforeDisconnect();
        await _closeTokenSource.CancelAsync();
        await Channel.CloseAsync( disconnectReason );
        return true;
    }

    protected async ValueTask PumpsDisconnectAsync( DisconnectReason disconnectedReason )
    {
        await CloseAsync( () => new ValueTask(), disconnectedReason );
        Sink.OnUnattendedDisconnect( disconnectedReason );
    }

    /// <summary>
    /// Will wait on pumps tasks, must not be run from a pump !
    /// </summary>
    /// <param name="clearSession"></param>
    /// <param name="disconnectReason"></param>
    /// <returns></returns>
    protected async Task<bool> DoDisconnectAsync( bool clearSession, DisconnectReason disconnectReason )
    {
        return await CloseAsync( async () =>
        {
            // We need that the pump finish their work so they don't write/read anything.
            var inputPump = InputPump;
            var outputPump = OutputPump;
            if( inputPump?.WorkTask != null ) await inputPump.WorkTask;
            if( outputPump?.WorkTask != null ) await outputPump.WorkTask;
            var channel = Channel;
            var duplexPipe = channel.DuplexPipe;
            await BeforeDisconnectAsync( duplexPipe!, clearSession );
            if( clearSession )
            {
                await LocalPacketStore.ResetAsync();
            }
        }, disconnectReason );
    }

    protected virtual ValueTask BeforeDisconnectAsync( IDuplexPipe duplexPipe, bool clearSession ) => new();

    public virtual ValueTask DisposeAsync()
    {
        Channel.Dispose();
        _stopTokenSource.Dispose();
        return ValueTask.CompletedTask;
    }
}




