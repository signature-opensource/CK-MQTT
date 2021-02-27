using CK.Core;
using CK.MQTT.Stores;
using System;
using System.Buffers;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CK.MQTT.OutputPump;

namespace CK.MQTT
{
    public class MainOutputProcessor
    {
        readonly MqttConfiguration _config;
        readonly IMqttIdStore _packetStore;
        readonly PingRespReflex _pingRespReflex;
        readonly IStopwatch _stopwatch;
        public MainOutputProcessor( MqttConfiguration config, IMqttIdStore packetStore, PingRespReflex pingRespReflex )
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
            // This function will be called again immediately upon return, if the client is not closing.
            using( IDisposableGroup? mainGrpDispose = m?.MainOutputProcessorLoop() )
            {
                if( IsPingReqTimeout ) // Because we are in a loop, this will be called immediately after a return. Keep this in mind.
                {
                    await clientClose( DisconnectedReason.PingReqTimeout );
                    m?.ConcludeMainLoopTimeout( mainGrpDispose! );
                    return;
                }
                // Because the config can change dynamically, we copy these values to avoid bugs.
                int keepAlive = _config.KeepAliveSeconds * 1000;
                if( keepAlive == 0 ) keepAlive = int.MaxValue;
                int waitTimeout = _config.WaitTimeoutMilliseconds;

                // Prioritization: ...
                bool packetSent = await SendAMessageFromQueue( m, sender, reflexes, messages ); // We want to send a fresh new packet...
                TimeSpan timeToNextResend = await ResendAllUnackPacket( m, sender ); // Then sending all packets that waited for too long.

                // Here we sent all unack packet, it mean the only messages available right are the one in the queue.
                if( packetSent )
                {
                    m?.ConcludeRegularPacketSent( mainGrpDispose! );
                    return;
                }
                m?.AwaitingWork();
                // But if we didn't sent any message from the queue, it mean that we have no more messages to send.
                // We need to wait for a new packet to send, or send a PingReq if didn't sent a message for too long and check if the broker did answer.
                // This chunk does a lot of 'slow' things, but we don't care since the output pump have nothing else to do.

                // Loop until we reached the time to send a packet.
                // Or if keepalive is disabled (infinite), in this case, we don't want to exit this loop until a packet to send is available
                while( keepAlive > 0 || keepAlive != int.MaxValue )
                {
                    // If we wait for too long, we may miss things like sending a keepalive, so we need to compute the minimal amount of time we have to wait.
                    int timeToWait = Math.Min( (int)timeToNextResend.TotalMilliseconds, keepAlive );
                    if( _pingRespReflex.WaitingPingResp ) timeToWait = Math.Min( timeToWait, waitTimeout );

                    Task<bool> reflexesWait = reflexes.Reader.WaitToReadAsync().AsTask();
                    Task<bool> messagesWait = messages.Reader.WaitToReadAsync().AsTask();
                    Task packetMarkedAsDropped = _packetStore.GetTaskResolvedOnPacketDropped();
                    Task timeToWaitTask = _config.DelayHandler.Delay( timeToWait, cancellationToken );
                    _ = await Task.WhenAny( timeToWaitTask, reflexesWait, messagesWait, packetMarkedAsDropped );
                    if( IsPingReqTimeout )
                    {
                        m?.ConcludeMainLoopTimeout( mainGrpDispose! );
                        await clientClose( DisconnectedReason.PingReqTimeout );
                        return;
                    }
                    if( cancellationToken.IsCancellationRequested )
                    {
                        m?.ConcludeMainLoopCancelled( mainGrpDispose! );
                        return;
                    }
                    if( reflexesWait.IsCompleted || messagesWait.IsCompleted ) // a message in a queue is available.
                    {
                        m?.ConcludeMessageInQueueAvailable( mainGrpDispose! );
                        return;
                    }
                    if( packetMarkedAsDropped.IsCompleted ) // A packet has been marked as dropped and must be resent.
                    {
                        m?.ConcludePacketDroppedAvailable( mainGrpDispose! );
                        return;
                    }
                    if( keepAlive != int.MaxValue ) keepAlive -= timeToWait;
                    // Maybe we waited something else than the keepalive (unack packets, timeout).
                    // So we subtract the time we waited to the keepalive, and run the whole loop again.
                }
                //keepAlive reached 0. So we must send a ping.
                using( m?.MainLoopSendingKeepAlive() )
                {
                    await sender( m, OutgoingPingReq.Instance );
                }
                _stopwatch.Restart();
                _pingRespReflex.WaitingPingResp = true;
                m?.ConcludeSentKeepAlive( mainGrpDispose! );
                return;
            }
        }

        static async ValueTask<bool> SendAMessageFromQueue( IOutputLogger? m, PacketSender packetSender, Channel<IOutgoingPacket> reflexes, Channel<IOutgoingPacket> messages )
        {
            if( !reflexes.Reader.TryRead( out IOutgoingPacket packet ) && !messages.Reader.TryRead( out packet ) )
            {
                m?.QueueEmpty();
                return false;
            }
            using( m?.SendingMessageFromQueue() )
            {
                await packetSender( m, packet );
                return true;
            }
        }

        async ValueTask<TimeSpan> ResendAllUnackPacket( IOutputLogger? m, PacketSender packetSender )
        {
            if( _config.WaitTimeoutMilliseconds == int.MaxValue ) // Resend is disabled.
            {
                return Timeout.InfiniteTimeSpan;
            }

            (IOutgoingPacket? outgoingPacket, TimeSpan timeUntilAnotherRetry) = await _packetStore.GetPacketToResend();
            if( outgoingPacket is null )
            {
                m?.NoUnackPacketSent( timeUntilAnotherRetry );
                return timeUntilAnotherRetry;
            }
            using( IDisposableGroup? grp = m?.ResendAllUnackPacket() )
            {
                await packetSender( m, outgoingPacket );
                while( true )
                {
                    (outgoingPacket, timeUntilAnotherRetry) = await _packetStore.GetPacketToResend();
                    if( outgoingPacket is null )
                    {
                        m?.ConcludeTimeUntilNextUnackRetry( grp, timeUntilAnotherRetry );
                        return timeUntilAnotherRetry;
                    }
                    await packetSender( m, outgoingPacket );
                }
            }
        }
    }
}
