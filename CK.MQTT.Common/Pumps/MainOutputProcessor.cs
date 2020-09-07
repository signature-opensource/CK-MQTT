using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CK.MQTT.OutputPump;

namespace CK.MQTT.Common.Pumps
{
    public class MainOutputProcessor
    {
        readonly MqttConfiguration _config;
        readonly PacketStore _packetStore;
        readonly PingRespReflex _pingRespReflex;

        public MainOutputProcessor( MqttConfiguration config, PacketStore packetStore, PingRespReflex pingRespReflex )
        {
            _config = config;
            _packetStore = packetStore;
            _pingRespReflex = pingRespReflex;
        }

        public async ValueTask OutputProcessor( IOutputLogger? m, PacketSender packetSender, Channel<IOutgoingPacket> reflexes, Channel<IOutgoingPacket> messages, CancellationToken cancellationToken )
        {
            bool messageSent = await SendAMessageFromQueue( m, packetSender, reflexes, messages );
            TimeSpan timeToNewPacket = await ResendUnackPacket( m, packetSender, messages, cancellationToken );//We empty the retries.
            if( messageSent ) return;//We sent a packet, there is maybe more in stock, no need to go in the *heavy* wait logic !
            //No packet was sent, we need to wait a new one. This part is heavy, but we don't care since the output pump have nothing else to do.
            Task keepAliveTask = Task.Delay( _config.KeepAlive, cancellationToken );//Cancellation token to cancel WhenAny.
            Task unackPacketDueTask = Task.Delay( timeToNewPacket );
            ValueTask<bool> reflexTask = reflexes.Reader.WaitToReadAsync();
            ValueTask<bool> messageTask = messages.Reader.WaitToReadAsync();
            await Task.WhenAny( keepAliveTask, unackPacketDueTask, reflexTask.AsTask(), messageTask.AsTask() );
            
            if( !keepAliveTask.IsCompleted ) return; //if something else than keepAlive is completed, it will be sent when this will be called again.
            //If the keepalive is completed, we must send a PingReq.
            await packetSender( m, OutgoingPingReq.Instance );
            _pingRespReflex.StartPingTimeoutTimer();
        }

        static async ValueTask<bool> SendAMessageFromQueue( IOutputLogger? m, PacketSender packetSender, Channel<IOutgoingPacket> reflexes, Channel<IOutgoingPacket> messages )
        {
            if( !reflexes.Reader.TryRead( out IOutgoingPacket packet ) && !messages.Reader.TryRead( out packet ) ) return false;
            await packetSender( m, packet );
            return true;
        }

        async ValueTask<TimeSpan> ResendUnackPacket( IOutputLogger? m, PacketSender packetSender, Channel<IOutgoingPacket> messages, CancellationToken cancellationToken )
        {
            if( _config.WaitTimeout == Timeout.InfiniteTimeSpan ) return Timeout.InfiniteTimeSpan;//Resend is disabled.
            while( true )
            {
                (int packetId, TimeSpan waitTime) = _packetStore.IdStore.GetOldestPacket();
                //0 mean there is no packet in the store. So we don't want to wake up the loop to resend packets.
                if( packetId == 0 ) return Timeout.InfiniteTimeSpan;//No packet in store, so we don't want to wake up for this.
                if( waitTime < _config.WaitTimeout ) return _config.WaitTimeout - waitTime;//Wait the right amount of time
                await packetSender( m, await _packetStore.GetMessageByIdAsync( m, packetId ) );
                _packetStore.IdStore.PacketSent( m, packetId );//We reset the timer, or this packet will be picked up again.
            }
        }
    }
}
