using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CK.MQTT.OutputPump;

namespace CK.MQTT
{
    public class MainOutputProcessor
    {
        readonly MqttClient _client;
        readonly MqttConfiguration _config;
        readonly PacketStore _packetStore;
        readonly PingRespReflex _pingRespReflex;
        readonly Stopwatch _stopwatch = new Stopwatch();
        public MainOutputProcessor( MqttClient client, MqttConfiguration config, PacketStore packetStore, PingRespReflex pingRespReflex )
        {
            _client = client;
            _config = config;
            _packetStore = packetStore;
            _pingRespReflex = pingRespReflex;
        }

        async Task<bool> TestPingTimeout()
        {
            if( !_pingRespReflex.WaitingPingResp || _stopwatch.Elapsed <= _config.WaitTimeout ) return false;
            await _client.CloseSelfAsync( DisconnectedReason.PingReqTimeout );
            return true;
        }

        TimeSpan Min( TimeSpan a, TimeSpan b ) => a < b ? a : b;

        public async ValueTask OutputProcessor( IOutputLogger? m, OutputPump outputPump, PacketSender packetSender, Channel<IOutgoingPacket> reflexes, Channel<IOutgoingPacket> messages, CancellationToken cancellationToken )
        {
            // Before sending a packet, we check that a PingReq did not timeout. 
            if( await TestPingTimeout() ) return; // We may have sent a ping, then outgoing packets were available to send so the post wait logic was not executed.
            bool messageSent = await SendAMessageFromQueue( m, packetSender, reflexes, messages );
            // We capture these values, so they wont change in the middle of the process.
            TimeSpan keepAlive = _config.KeepAlive;
            TimeSpan waitTimeout = _config.WaitTimeout;
            TimeSpan timeToNextResend = await ResendUnackPacket( m, packetSender, waitTimeout );//We send all packets that waited for too long.
            if( messageSent ) return; //We sent a packet, but there is maybe more to send.
            //No packet was sent, so we need to wait a new packet.
            //This chunk does a lot of 'slow' things, but we don't care since the output pump have nothing else to do.

            // We compute the time we will have to wait.
            // If we wait for too long, we may miss things like sending a keepalive, so we need to compute the minimal amount of time we have to wait.
            TimeSpan timeToWait = Min( timeToNextResend, keepAlive );// We need to send a PingReq or resending the unack packets...
            if( _pingRespReflex.WaitingPingResp ) timeToWait = Min( timeToWait, waitTimeout );//... but if we are waiting a PingResp, we may timeout before sending a new packet.

            ValueTask<bool> reflexesWait = reflexes.Reader.WaitToReadAsync();
            ValueTask<bool> messagesWait = messages.Reader.WaitToReadAsync();
            await Task.WhenAny( Task.Delay( timeToWait, cancellationToken ), reflexesWait.AsTask(), messagesWait.AsTask() );
            if( await TestPingTimeout() ) return;//Now, we exit if we did timeout.
            //Now we need to know if we must send a keepalive.
            if( reflexesWait.IsCompleted || messagesWait.IsCompleted || HaveUnackPacketToSend(waitTimeout) ) return;//These guy have message available.
            if( timeToNextResend < keepAlive )
                //TODO: here, we may have waited a packet to resend, but lost track at how much time we need to wait for the ping.


                if( !keepAliveTask.IsCompleted ) return; //if something else than keepAlive is completed, it will be sent when this will be called again.
                                                         //If the keepalive is completed, we must send a PingReq.
            await packetSender( m, OutgoingPingReq.Instance );
            _stopwatch.Restart();
            _pingRespReflex.WaitingPingResp = true;
            return;
        }

        static async ValueTask<bool> SendAMessageFromQueue( IOutputLogger? m, PacketSender packetSender, Channel<IOutgoingPacket> reflexes, Channel<IOutgoingPacket> messages )
        {
            if( !reflexes.Reader.TryRead( out IOutgoingPacket packet ) && !messages.Reader.TryRead( out packet ) ) return false;
            await packetSender( m, packet );
            return true;
        }

        bool HaveUnackPacketToSend( TimeSpan waitTimeout )
        {
            (int packetId, TimeSpan waitTime) = _packetStore.IdStore.GetOldestPacket();
            return packetId != 0 && waitTime < waitTimeout;
        }

        async ValueTask<TimeSpan> ResendUnackPacket( IOutputLogger? m, PacketSender packetSender, TimeSpan waitTimeout )
        {
            if( _config.WaitTimeout == Timeout.InfiniteTimeSpan ) return Timeout.InfiniteTimeSpan;//Resend is disabled.
            while( true )
            {
                (int packetId, TimeSpan waitTime) = _packetStore.IdStore.GetOldestPacket();
                //0 mean there is no packet in the store. So we don't want to wake up the loop to resend packets.
                if( packetId == 0 ) return Timeout.InfiniteTimeSpan;//No packet in store, so we don't want to wake up for this.
                if( waitTime < waitTimeout ) return waitTimeout - waitTime;//Wait the right amount of time
                await packetSender( m, await _packetStore.GetMessageByIdAsync( m, packetId ) );
                _packetStore.IdStore.PacketSent( m, packetId );//We reset the timer, or this packet will be picked up again.
            }
        }
    }
}
