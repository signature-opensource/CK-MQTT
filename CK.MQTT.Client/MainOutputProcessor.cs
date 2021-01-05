using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CK.MQTT.OutputPump;

namespace CK.MQTT
{
    public class MainOutputProcessor
    {
        readonly MqttConfiguration _config;
        readonly PacketStore _packetStore;
        readonly PingRespReflex _pingRespReflex;
        readonly IStopwatch _stopwatch;
        public MainOutputProcessor( MqttConfiguration config, PacketStore packetStore, PingRespReflex pingRespReflex )
        {
            _stopwatch = config.StopwatchFactory.Create();
            (_config, _packetStore, _pingRespReflex) = (config, packetStore, pingRespReflex);
        }

        bool IsPingReqTimeout =>
            _pingRespReflex.WaitingPingResp
            && _stopwatch.Elapsed.TotalMilliseconds > _config.WaitTimeoutMilliseconds
            && _config.WaitTimeoutMilliseconds != int.MaxValue; //We never timeout if it's configured to int.MaxValue.

        public async ValueTask OutputProcessor(
            IOutputLogger? m,
            PacketSender sender,
            Channel<IOutgoingPacket> reflexes,
            Channel<IOutgoingPacket> messages,
            Func<DisconnectedReason, Task> clientClose,
            CancellationToken cancellationToken )
        {
            // This is really easy to put bug in this function, thats why this is heavily commented.
            // This function will be called again immediatly upon return, if the client is not closing.

            if( IsPingReqTimeout ) // Because we are in a loop, this will be called immediatly after a return. Keep this in mind.
            {
                await clientClose( DisconnectedReason.PingReqTimeout );
                return;
            }
            // Because the config can change dynamically, we copy these values to avoid bugs.
            int keepAlive = _config.KeepAliveSeconds * 1000;
            if( keepAlive == 0 ) keepAlive = int.MaxValue;
            int waitTimeout = _config.WaitTimeoutMilliseconds;

            // Prioritization: ...
            bool packetSent = await SendAMessageFromQueue( m, sender, reflexes, messages ); // We want to send a fresh new packet...
            int timeToNextResend = await ResendAllUnackPacket( m, sender, waitTimeout ); // Then sending all packets that waited for too long.

            // Here we sent all unack packet, it mean the only messages availables right are the one in the queue.
            if( packetSent ) return;
            // But if we didn't sent any message from the queue, it mean that we have no more messages to send.
            // We need to wait for a new packet to send, or send a PingReq if didn't sent a message for too long and check if the broker did answer.
            // This chunk does a lot of 'slow' things, but we don't care since the output pump have nothing else to do.

            // Loop until we reached the time to send a packet.
            // Or if keepalive is disabled (infinite), in this case, we don't want to exit this loop until a packet to send is available
            while( keepAlive > 0 || keepAlive != int.MaxValue )
            {
                // If we wait for too long, we may miss things like sending a keepalive, so we need to compute the minimal amount of time we have to wait.
                int timeToWait = Math.Min( timeToNextResend, keepAlive );
                if( _pingRespReflex.WaitingPingResp ) timeToWait = Math.Min( timeToWait, waitTimeout );

                Task<bool> reflexesWait = reflexes.Reader.WaitToReadAsync().AsTask();
                Task<bool> messagesWait = messages.Reader.WaitToReadAsync().AsTask();
                await Task.WhenAny( _config.DelayHandler.Delay( timeToWait, cancellationToken ), reflexesWait, messagesWait );
                if( IsPingReqTimeout )
                {
                    await clientClose( DisconnectedReason.PingReqTimeout );
                    return;
                }
                if( reflexesWait.IsCompleted //because we have a message in a queue.
                    || messagesWait.IsCompleted
                    || HaveUnackPacketToSend( waitTimeout ) // or we have a packet to re-send.
                    || cancellationToken.IsCancellationRequested )// or the operation is cancelled.
                {
                    return;
                }
                if( keepAlive != int.MaxValue ) keepAlive -= timeToWait;
                // Maybe we waited something else than the keepalive (unack packets, timeout).
                // So we substract the time we waited to the keepalive, and run the whole loop again.
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

        bool HaveUnackPacketToSend( int waitTimeout )
        {
            (int packetId, int waitTime) = _packetStore.IdStore.GetOldestUnackedPacket();
            return packetId != 0 && waitTime < waitTimeout;
        }

        async ValueTask<int> ResendAllUnackPacket( IOutputLogger? m, PacketSender packetSender, int waitTimeout )
        {
            if( _config.WaitTimeoutMilliseconds == int.MaxValue )
            {
                // Resend is disabled.
                return int.MaxValue;
            }
            while( true )
            {
                (int packetId, int waitTime) = _packetStore.IdStore.GetOldestUnackedPacket();
                // 0 means that there is no packet in the store. So we don't want to wake up the loop to resend packets.
                if( packetId == 0 ) return int.MaxValue;
                // Wait the right amount of time.
                if( waitTime < waitTimeout ) return waitTimeout - waitTime;
                await packetSender( m, await _packetStore.GetMessageByIdAsync( m, packetId ) );

            }
        }
    }
}
