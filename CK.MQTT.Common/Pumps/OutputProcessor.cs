using CK.Core;
using CK.MQTT.Stores;
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
        readonly IOutgoingPacketStore _outgoingPacketStore;

        public OutputProcessor( OutputPump outputPump, PipeWriter pipeWriter, IOutgoingPacketStore outgoingPacketStore )
        {
            OutputPump = outputPump;
            _pipeWriter = pipeWriter;
            _outgoingPacketStore = outgoingPacketStore;
        }

        TimeSpan _timeUntilNextRetry = TimeSpan.MaxValue;

        public virtual async ValueTask<bool> SendPackets( IOutputLogger? m, CancellationToken cancellationToken )
        {
            using( m?.OutputProcessorRunning() )
            {

                bool newPacketSent = await SendAMessageFromQueue( m, cancellationToken ); // We want to send a fresh new packet...
                bool retriesSent = await ResendAllUnackPacket( m, cancellationToken ); // Then sending all packets that waited for too long.
                bool packetSent = newPacketSent || retriesSent;
                return packetSent;
            }

        }
        public virtual async Task WaitPacketAvailableToSendAsync( IOutputLogger? m, CancellationToken cancellationToken )
        {
            using( IDisposableGroup? grp = m?.AwaitingWork() )
            {

                // If we wait for too long, we may miss things like sending a keepalive, so we need to compute the minimal amount of time we have to wait.
                Task<bool> reflexesWait = OutputPump.ReflexesChannel.Reader.WaitToReadAsync().AsTask();
                Task<bool> messagesWait = OutputPump.MessagesChannel.Reader.WaitToReadAsync().AsTask();
                Task packetMarkedAsDropped = _outgoingPacketStore.GetTaskResolvedOnPacketDropped();
                Task timeToWaitForRetry = OutputPump.Config.DelayHandler.Delay( _timeUntilNextRetry, cancellationToken );
                _ = await Task.WhenAny( timeToWaitForRetry, reflexesWait, messagesWait, packetMarkedAsDropped );
                m?.AwaitCompletedDueTo( grp, reflexesWait, messagesWait, packetMarkedAsDropped, timeToWaitForRetry );
            }
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

            (IOutgoingPacket? outgoingPacket, TimeSpan timeUntilAnotherRetry) = await _outgoingPacketStore.GetPacketToResend();
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
                    (outgoingPacket, timeUntilAnotherRetry) = await _outgoingPacketStore.GetPacketToResend();
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
            using( m?.SendingMessage( ref outgoingPacket, OutputPump.PConfig.ProtocolLevel ) )
            {
                if( outgoingPacket.Qos != QualityOfService.AtMostOnce )
                {
                    // This must be done BEFORE writing the packet to avoid concurrency issues.
                    _outgoingPacketStore.OnPacketSent( m, outgoingPacket.PacketId );
                    // Explanation:
                    // The receiver and input loop can run before the next line is executed.
                }
                await outgoingPacket.WriteAsync( OutputPump.PConfig.ProtocolLevel, _pipeWriter, cancellationToken );
            }
        }
    }
}
