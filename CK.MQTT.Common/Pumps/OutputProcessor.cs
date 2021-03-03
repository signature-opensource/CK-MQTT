using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Pumps
{
    public class OutputProcessor
    {
        protected OutputPump OutputPump { get; }
        readonly PipeWriter _pipeWriter;

#if DEBUG
        bool _waitLeadToPacketSent;
#endif

        public OutputProcessor( OutputPump outputPump, PipeWriter pipeWriter )
        {
            OutputPump = outputPump;
            _pipeWriter = pipeWriter;
        }

        TimeSpan _timeUntilNextRetry = TimeSpan.MaxValue;

        public virtual async ValueTask<bool> SendPackets( IOutputLogger? m, CancellationToken cancellationToken )
        {
            bool newPacketSent = await SendAMessageFromQueue( m, cancellationToken ); // We want to send a fresh new packet...
            bool retriesSent = await ResendAllUnackPacket( m, cancellationToken ); // Then sending all packets that waited for too long.
            bool packetSent = newPacketSent || retriesSent;
#if DEBUG
            if( _waitLeadToPacketSent ) // Test the guarentee that after a wait there is always a packet sent.
            {
                Debug.Assert( packetSent );
                _waitLeadToPacketSent = false;
            }
#endif
            return packetSent;
        }
        public virtual async Task WaitPacketAvailableToSendAsync( IOutputLogger? m, CancellationToken cancellationToken )
        {
            m?.AwaitingWork();
            // If we wait for too long, we may miss things like sending a keepalive, so we need to compute the minimal amount of time we have to wait.
            Task<bool> reflexesWait = OutputPump.ReflexesChannel.Reader.WaitToReadAsync().AsTask();
            Task<bool> messagesWait = OutputPump.MessagesChannel.Reader.WaitToReadAsync().AsTask();
            Task packetMarkedAsDropped = OutputPump.Store.GetTaskResolvedOnPacketDropped();
            Task timeToWaitForRetry = OutputPump.Config.DelayHandler.Delay( _timeUntilNextRetry, cancellationToken );
            _ = await Task.WhenAny( timeToWaitForRetry, reflexesWait, messagesWait, packetMarkedAsDropped );
            _waitLeadToPacketSent = true;
        }

        public void Stopping() => _pipeWriter.Complete();

        async ValueTask<bool> SendAMessageFromQueue( IOutputLogger? m, CancellationToken cancellationToken )
        {
            if( !OutputPump.ReflexesChannel.Reader.TryRead( out IOutgoingPacket packet ) && !OutputPump.MessagesChannel.Reader.TryRead( out packet ) )
            {
                m?.QueueEmpty();
                return false;
            }
            using( m?.SendingMessageFromQueue() )
            {
                await ProcessOutgoingPacket( m, packet, cancellationToken );
                return true;
            }
        }

        async ValueTask<bool> ResendAllUnackPacket( IOutputLogger? m, CancellationToken cancellationToken )
        {
            if( OutputPump.Config.WaitTimeoutMilliseconds == int.MaxValue ) return false; // Resend is disabled.

            (IOutgoingPacket? outgoingPacket, TimeSpan timeUntilAnotherRetry) = await OutputPump.Store.GetPacketToResend();
            if( outgoingPacket is null )
            {
                m?.NoUnackPacketSent( timeUntilAnotherRetry );
                _timeUntilNextRetry = timeUntilAnotherRetry;
                return false;
            }
            using( IDisposableGroup? grp = m?.ResendAllUnackPacket() )
            {
                await ProcessOutgoingPacket( m, outgoingPacket, cancellationToken );
                while( true )
                {
                    (outgoingPacket, timeUntilAnotherRetry) = await OutputPump.Store.GetPacketToResend();
                    if( outgoingPacket is null )
                    {
                        m?.ConcludeTimeUntilNextUnackRetry( grp!, timeUntilAnotherRetry );
                        _timeUntilNextRetry = timeUntilAnotherRetry;
                        return true;
                    }
                    await ProcessOutgoingPacket( m, outgoingPacket, cancellationToken );
                }
            }
        }

        protected async ValueTask ProcessOutgoingPacket( IOutputLogger? m, IOutgoingPacket outgoingPacket, CancellationToken cancellationToken )
        {
            if( cancellationToken.IsCancellationRequested ) return;
            if( outgoingPacket is IOutgoingPacketWithId packetWithId )
            {
                using( m?.SendingMessageWithId( ref outgoingPacket, OutputPump.PConfig.ProtocolLevel, packetWithId.PacketId ) )
                {
                    OutputPump.Store.OnPacketSent( m, packetWithId.PacketId ); // This should be done BEFORE writing the packet to avoid concurrency issues.
                    // Explanation:
                    // Sometimes, the receiver and input loop is faster than simply running "OnPacketSent".
                    await outgoingPacket.WriteAsync( OutputPump.PConfig.ProtocolLevel, _pipeWriter, cancellationToken );
                }
            }
            else
            {
                using( m?.SendingMessage( ref outgoingPacket, OutputPump.PConfig.ProtocolLevel ) )
                {
                    await outgoingPacket.WriteAsync( OutputPump.PConfig.ProtocolLevel, _pipeWriter, cancellationToken );
                }
            }

        }
    }
}
