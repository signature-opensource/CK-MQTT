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

        readonly ProtocolConfiguration _pConfig;
        readonly PipeWriter _pipeWriter;
        readonly IOutgoingPacketStore _outgoingPacketStore;

        public OutputProcessor( ProtocolConfiguration pConfig, OutputPump outputPump, PipeWriter pipeWriter, IOutgoingPacketStore outgoingPacketStore )
        {
            _pConfig = pConfig;
            OutputPump = outputPump;
            _pipeWriter = pipeWriter;
            _outgoingPacketStore = outgoingPacketStore;
        }

        TimeSpan _timeUntilNextRetry = TimeSpan.MaxValue;

        public virtual async ValueTask<bool> SendPacketsAsync( IOutputLogger? m, CancellationToken cancellationToken )
        {
            using( m?.OutputProcessorRunning() )
            {
                bool newPacketSent = await SendAMessageFromQueueAsync( m, cancellationToken ); // We want to send a fresh new packet...
                bool retriesSent = await ResendAllUnackPacketAsync( m, cancellationToken ); // Then sending all packets that waited for too long.
                bool packetSent = newPacketSent || retriesSent;
                return packetSent;
            }

        }
        public virtual async Task WaitPacketAvailableToSendAsync( IOutputLogger? m, CancellationToken cancellationToken )
        {
            using( IDisposableGroup? grp = m?.AwaitingWork() )
            {
                // If we wait for too long, we may miss things like sending a keepalive, so we need to compute the minimal amount of time we have to wait.
                CancellationToken cancelOnPacketDropped = _outgoingPacketStore.DroppedPacketCancelToken;

                using( var ctsRetry = OutputPump.Config.CancellationTokenSourceFactory.Create( _timeUntilNextRetry ) )
                using( var linkedCts = CancellationTokenSource.CreateLinkedTokenSource( ctsRetry.Token, cancelOnPacketDropped, cancellationToken ) )
                {
                    Task<bool> reflexesWait = OutputPump.ReflexesChannel.Reader.WaitToReadAsync( linkedCts.Token ).AsTask();
                    Task<bool> messagesWait = OutputPump.MessagesChannel.Reader.WaitToReadAsync( linkedCts.Token ).AsTask();
                    await Task.WhenAny( reflexesWait, messagesWait );
                    m?.AwaitCompletedDueTo( grp, reflexesWait, messagesWait, ctsRetry.Token, cancelOnPacketDropped, cancellationToken );
                    linkedCts.Cancel(); //Avoid leaking useless background task.
                }
            }
        }

        internal void Stopping() => _pipeWriter.Complete();

        protected ValueTask SelfDisconnectAsync( DisconnectedReason disconnectedReason ) => OutputPump.SelfCloseAsync( disconnectedReason );

        async ValueTask<bool> SendAMessageFromQueueAsync( IOutputLogger? m, CancellationToken cancellationToken )
        {
            if( !OutputPump.ReflexesChannel.Reader.TryRead( out IOutgoingPacket packet ) && !OutputPump.MessagesChannel.Reader.TryRead( out packet ) )
            {
                m?.QueueEmpty();
                return false;
            }
            using( m?.SendingMessageFromQueue() )
            {
                await ProcessOutgoingPacketAsync( m, packet, cancellationToken );
                return true;
            }
        }

        async ValueTask<bool> ResendAllUnackPacketAsync( IOutputLogger? m, CancellationToken cancellationToken )
        {
            if( OutputPump.Config.WaitTimeoutMilliseconds == int.MaxValue ) return false; // Resend is disabled.

            (IOutgoingPacket? outgoingPacket, TimeSpan timeUntilAnotherRetry) = await _outgoingPacketStore.GetPacketToResendAsync();
            if( outgoingPacket is null )
            {
                m?.NoUnackPacketSent( timeUntilAnotherRetry );
                _timeUntilNextRetry = timeUntilAnotherRetry;
                return false;
            }
            using( IDisposableGroup? grp = m?.ResendAllUnackPacket() )
            {
                await ProcessOutgoingPacketAsync( m, outgoingPacket, cancellationToken );
                while( true )
                {
                    (outgoingPacket, timeUntilAnotherRetry) = await _outgoingPacketStore.GetPacketToResendAsync();
                    if( outgoingPacket is null )
                    {
                        m?.ConcludeTimeUntilNextUnackRetry( grp!, timeUntilAnotherRetry );
                        _timeUntilNextRetry = timeUntilAnotherRetry;
                        return true;
                    }
                    await ProcessOutgoingPacketAsync( m, outgoingPacket, cancellationToken );
                }
            }
        }

        protected async ValueTask ProcessOutgoingPacketAsync( IOutputLogger? m, IOutgoingPacket outgoingPacket, CancellationToken cancellationToken )
        {
            if( cancellationToken.IsCancellationRequested ) return;
            using( m?.SendingMessage( ref outgoingPacket, _pConfig.ProtocolLevel ) )
            {
                if( outgoingPacket.Qos != QualityOfService.AtMostOnce )
                {
                    // This must be done BEFORE writing the packet to avoid concurrency issues.
                    _outgoingPacketStore.OnPacketSent( m, outgoingPacket.PacketId );
                    // Explanation:
                    // The receiver and input loop can run before the next line is executed.
                }
                await outgoingPacket.WriteAsync( _pConfig.ProtocolLevel, _pipeWriter, cancellationToken );
            }
        }
    }
}
