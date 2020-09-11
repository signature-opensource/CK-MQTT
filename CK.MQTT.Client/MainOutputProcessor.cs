using System;
using System.Diagnostics;
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

        bool IsPingReqTimeout => _pingRespReflex.WaitingPingResp && _stopwatch.Elapsed > _config.WaitTimeout;

        TimeSpan Min( TimeSpan a, TimeSpan b )
        {
            if( a == Timeout.InfiniteTimeSpan ) return b;
            if( b == Timeout.InfiniteTimeSpan ) return a;
            return a < b ? a : b;
        }

        public async ValueTask OutputProcessor(
            IOutputLogger? m, PacketSender sender, Channel<IOutgoingPacket> reflexes, Channel<IOutgoingPacket> messages, CancellationToken cancellationToken, Func<DisconnectedReason, Task> _clientClose
        )
        {
            // This is really easy to put bug in this function, thats why this is heavily commented.
            // This function will be called again immediatly upon return, if the client is not closing.

            if( IsPingReqTimeout ) // Because we are in a loop, this will be called immediatly after a return. Keep this in mind.
            {
                await _clientClose( DisconnectedReason.PingReqTimeout );
                return;
            }
            // Because the config can change dynamically, we copy these values to avoid bugs.
            TimeSpan keepAlive = _config.KeepAlive;
            TimeSpan waitTimeout = _config.WaitTimeout;

            // Prioritization: ...
            bool packetSent = await SendAMessageFromQueue( m, sender, reflexes, messages ); // We want to send a fresh new packet...
            TimeSpan timeToNextResend = await ResendAllUnackPacket( m, sender, waitTimeout ); // Then sending all packets that waited for too long.
            // Here we sent all unack packet, it mean the only messages availables right are the one in the queue.
            if( packetSent ) return;
            // But if we didn't sent any message from the queue, it mean that we have no more messages to send.
            // We need to wait for a new packet to send, or send a PingReq if didn't sent a message for too long and check if the broker did answer.
            // This chunk does a lot of 'slow' things, but we don't care since the output pump have nothing else to do.

            // Loop until we reached the time to send the keepalive.
            // Or if keepalive is disabled (infinite), in this case, we don't want to exit this loop until a packet to send is available
            while( keepAlive.Ticks > 0 || keepAlive != Timeout.InfiniteTimeSpan )
            {
                // If we wait for too long, we may miss things like sending a keepalive, so we need to compute the minimal amount of time we have to wait.
                TimeSpan timeToWait = Min( timeToNextResend, keepAlive );
                if( _pingRespReflex.WaitingPingResp ) timeToWait = Min( timeToWait, waitTimeout );

                Task<bool> reflexesWait = reflexes.Reader.WaitToReadAsync().AsTask();
                Task<bool> messagesWait = messages.Reader.WaitToReadAsync().AsTask();
                await Task.WhenAny( Task.Delay( timeToWait, cancellationToken ), reflexesWait, messagesWait );
                if( IsPingReqTimeout )
                {
                    await _clientClose( DisconnectedReason.PingReqTimeout );
                    return;
                }
                if( reflexesWait.IsCompleted //because we have a message in a queue.
                    || messagesWait.IsCompleted
                    || HaveUnackPacketToSend( waitTimeout ) // or we have a packet to re-send.
                    || cancellationToken.IsCancellationRequested )// or the operation is cancelled.
                {
                    return;
                }
                keepAlive -= timeToWait;// Maybe we waited something else than the keepalive (unack packets, timeout).
                //So we substract the time we waited to the keepalive, and run the whole loop again.
            }
            //keepAlive reached 0. So we must send a ping.
            await sender( m, OutgoingPingReq.Instance );
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

        async ValueTask<TimeSpan> ResendAllUnackPacket( IOutputLogger? m, PacketSender packetSender, TimeSpan waitTimeout )
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
