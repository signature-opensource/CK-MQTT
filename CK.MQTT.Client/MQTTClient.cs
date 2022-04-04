using CK.Core;
using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.MQTT.Client.MQTTClientChannel;

namespace CK.MQTT.Client
{
    public class MQTTClient
    {
        readonly MQTTClientChannel _clientChannel = TODO;
        readonly PerfectEventSender<ApplicationMessage> _onMessageSender = new();
        readonly PerfectEventSender<DisconnectReason> _onConnectionChangeSender = new();
        readonly PerfectEventSender<ushort> _onStoreQueueFilling = new();

        public PerfectEvent<ApplicationMessage> OnMessage => _onMessageSender.PerfectEvent;

        public PerfectEvent<DisconnectReason> OnConnectionChange => _onConnectionChangeSender.PerfectEvent;

        public PerfectEvent<ushort> OnStoreQueueFilling => _onStoreQueueFilling.PerfectEvent;

        async Task WorkLoop()
        {
            ActivityMonitor m = new();
            await foreach( var item in _clientChannel.Messages.ReadAllAsync( TODO ) )
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
                } else
                {
                    m.Error( "Unknown event has been sent on the channel." );
                }
            }
        }
    }
}
