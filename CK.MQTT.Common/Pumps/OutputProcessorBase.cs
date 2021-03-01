using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Pumps
{
    class OutputProcessor
    {
        OutputPump _outputPump;

        public OutputProcessor( OutputPump outputPump )
        {
            _outputPump = outputPump;
        }

        public virtual async ValueTask<bool> SendPackets( IOutputLogger? m )
        {
            // Because the config can change dynamically, we copy these values to avoid bugs.
            int waitTimeout = _outputPump.Config.WaitTimeoutMilliseconds;

            // Prioritization: ...
            bool packetSent = await SendAMessageFromQueue( m, sender, reflexes, messages ); // We want to send a fresh new packet...
            TimeSpan timeToNextResend = await ResendAllUnackPacket( m, sender ); // Then sending all packets that waited for too long.
        }
        public virtual Task WaitPacketAvailableToSendAsync( IOutputLogger? m )
        {

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

        async ValueTask<TimeSpan> ResendAllUnackPacket( IOutputLogger? m )
        {
            if( _outputPump.Config.WaitTimeoutMilliseconds == int.MaxValue ) // Resend is disabled.
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

        async ValueTask ProcessOutgoingPacket( IOutputLogger? m, IOutgoingPacket outgoingPacket, CancellationToken cancellationToken )
        {
            if( cancellationToken.IsCancellationRequested ) return;
            if( outgoingPacket is IOutgoingPacketWithId packetWithId )
            {
                using( m?.SendingMessageWithId( ref outgoingPacket, _pconfig.ProtocolLevel, packetWithId.PacketId ) )
                {
                    Store.OnPacketSent( m, packetWithId.PacketId ); // This should be done BEFORE writing the packet to avoid concurrency issues.
                    // Explanation:
                    // Sometimes, the receiver and input loop is faster than simply running "OnPacketSent".
                    await outgoingPacket.WriteAsync( _pconfig.ProtocolLevel, _pipeWriter, StopToken );
                }
            }
            else
            {
                using( m?.SendingMessage( ref outgoingPacket, _pconfig.ProtocolLevel ) )
                {
                    await outgoingPacket.WriteAsync( _pconfig.ProtocolLevel, _pipeWriter, StopToken );
                }
            }

        }
    }
}
