using CK.Core;
using CK.MQTT.Packets;
using CK.PerfectEvent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public class MQTTClient : MQTTClientChannel
    {
        readonly PerfectEventSender<ApplicationMessage> _onMessageSender = new();
        readonly PerfectEventSender<DisconnectReason> _onConnectionChangeSender = new();
        readonly PerfectEventSender<ushort> _onStoreQueueFilling = new();

        public PerfectEvent<ApplicationMessage> OnMessage => _onMessageSender.PerfectEvent;

        public PerfectEvent<DisconnectReason> OnConnectionChange => _onConnectionChangeSender.PerfectEvent;

        public PerfectEvent<ushort> OnStoreQueueFilling => _onStoreQueueFilling.PerfectEvent;

        public MQTTClient( Mqtt3ClientConfiguration configuration ) : base( configuration )
        {
        }

        Task? _workLoop;
        public override async Task<ConnectResult> ConnectAsync( OutgoingLastWill? lastWill = null, CancellationToken cancellationToken = default )
        {
            var res = await base.ConnectAsync( lastWill, cancellationToken );
            _workLoop = WorkLoopAsync( Messages );
            return res;
        }

        public async Task<ConnectResult> ConnectAsync( bool waitForCompletion, OutgoingLastWill ? lastWill = null, CancellationToken cancellationToken = default )
        {
            var res = await base.ConnectAsync( lastWill, cancellationToken );
            _workLoop = WorkLoopAsync( Messages );
            if( !res.IsSuccess && waitForCompletion )
            {
                await _workLoop;
            }
            return res;
        }

        public override Task<bool> DisconnectAsync( bool deleteSession )
            => DisconnectAsync( deleteSession, true );


        /// <param name="deleteSession"></param>
        /// <param name="waitForCompletion">Wait the pump messages to empty all the messages.</param>
        /// <returns></returns>
        public async Task<bool> DisconnectAsync( bool deleteSession, bool waitForCompletion )
        {
            var res = await base.DisconnectAsync( deleteSession );
            if( waitForCompletion )
            {
                await _workLoop!;
            }
            return res;
        }


        async Task WorkLoopAsync( ChannelReader<object?> channel )
        {
            ActivityMonitor m = new();
            await foreach( var item in channel.ReadAllAsync() )
            {
                if( item is ApplicationMessage msg )
                {
                    await _onMessageSender.RaiseAsync( m, msg );
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
                else if( item is ReconnectionFailed reconnectionFailed )
                {
                    m.Warn( $"Reconnection attempt {reconnectionFailed.RetryCount}/{reconnectionFailed.MaxRetryCount} failed." );
                }
                else if( item is QueueFullPacketDestroyed queueFullPacketDestroyed )
                {
                    m.Warn( $"Because the queue is full, {queueFullPacketDestroyed.PacketType} with id {queueFullPacketDestroyed.PacketId} has been dropped. " );
                }
                else
                {
                    m.Error( "Unknown event has been sent on the channel." );
                }
            }
        }
    }
}
