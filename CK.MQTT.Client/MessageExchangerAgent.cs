using CK.Core;
using CK.MQTT.Packets;
using CK.PerfectEvent;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public class MessageExchangerAgent<T> : MessageExchangerAgentBase<T> where T : IConnectedMessageExchanger
    {
        readonly PerfectEventSender<ApplicationMessage> _onMessageSender = new();
        readonly PerfectEventSender<DisconnectReason> _onConnectionChangeSender = new();
        readonly PerfectEventSender<ushort> _onStoreQueueFilling = new();

        public PerfectEvent<ApplicationMessage> OnMessage => _onMessageSender.PerfectEvent;

        public PerfectEvent<DisconnectReason> OnConnectionChange => _onConnectionChangeSender.PerfectEvent;

        public PerfectEvent<ushort> OnStoreQueueFilling => _onStoreQueueFilling.PerfectEvent;

        protected Task? WorkLoop { get; set; }
        public MessageExchangerAgent( Func<IMqtt3Sink, T> clientFactory ) : base( clientFactory )
        {
        }

        [MemberNotNull( nameof( WorkLoop ) )]
        public override void Start( )
        {
            base.Start();
            WorkLoop ??= WorkLoopAsync( Messages.Reader );
        }

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


        protected async Task StopAsync( bool waitForCompletion )
        {
            Messages!.Writer.Complete();
            if( waitForCompletion )
            {
                await WorkLoop!;
            }
            Messages = null;
            WorkLoop = null;
        }

        virtual protected async Task WorkLoopAsync( ChannelReader<object?> channel )
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
