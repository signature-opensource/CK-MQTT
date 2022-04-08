using CK.MQTT.Packets;
using CK.MQTT.Stores;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Pumps
{
    public class OutputProcessor
    {
        protected OutputPump OutputPump { get; }

        readonly ProtocolConfiguration _pConfig;
        readonly PipeWriter _pipeWriter;
        readonly ILocalPacketStore _localPacketStore;

        public OutputProcessor( ProtocolConfiguration pConfig,
                               OutputPump outputPump,
                               PipeWriter pipeWriter,
                               ILocalPacketStore outgoingPacketStore )
        {
            _pConfig = pConfig;
            OutputPump = outputPump;
            _pipeWriter = pipeWriter;
            _localPacketStore = outgoingPacketStore;
        }

        TimeSpan _timeUntilNextRetry = TimeSpan.MaxValue;

        public virtual async ValueTask<bool> SendPacketsAsync( CancellationToken cancellationToken )
        {
            bool newPacketSent = await SendAMessageFromQueueAsync( cancellationToken ); // We want to send a fresh new packet...
            bool retriesSent = await ResendAllUnackPacketAsync( cancellationToken ); // Then sending all packets that waited for too long.
            return newPacketSent || retriesSent;
        }

        public virtual async Task WaitPacketAvailableToSendAsync( CancellationToken stopWaitToken, CancellationToken stopToken )
        {
            // If we wait for too long, we may miss things like sending a keepalive, so we need to compute the minimal amount of time we have to wait.
            CancellationToken cancelOnPacketDropped = _localPacketStore.DroppedPacketCancelToken;

            using( var ctsRetry = OutputPump.Config.CancellationTokenSourceFactory.Create( _timeUntilNextRetry ) )
            using( var linkedCts = CancellationTokenSource.CreateLinkedTokenSource( ctsRetry.Token, cancelOnPacketDropped, stopWaitToken ) )
            using( linkedCts.Token.Register( () => OutputPump.ReflexesChannel.Writer.TryWrite( OutputPump.FlushPacket.Instance ) ) )
            {
                await OutputPump.ReflexesChannel.Reader.WaitToReadAsync( stopToken );
            }
        }

        internal void Stopping() => _pipeWriter.Complete();

        protected ValueTask SelfDisconnectAsync( DisconnectReason disconnectedReason ) => OutputPump.SelfCloseAsync( disconnectedReason );

        protected virtual async ValueTask<bool> SendAMessageFromQueueAsync( CancellationToken cancellationToken )
        {
            IOutgoingPacket? packet;
            do
            {
                if( !OutputPump.ReflexesChannel.Reader.TryRead( out packet ) && !OutputPump.MessagesChannel.Reader.TryRead( out packet ) )
                {
                    return false;
                }
            }
            while( packet == OutputPump.FlushPacket.Instance );
            await ProcessOutgoingPacketAsync( packet, cancellationToken );
            return true;
        }

        async ValueTask<bool> ResendAllUnackPacketAsync( CancellationToken cancellationToken )
        {
            if( OutputPump.Config.WaitTimeoutMilliseconds == int.MaxValue ) return false; // Resend is disabled.

            (IOutgoingPacket? outgoingPacket, TimeSpan timeUntilAnotherRetry) = await _localPacketStore.GetPacketToResendAsync();
            if( outgoingPacket is null )
            {
                _timeUntilNextRetry = timeUntilAnotherRetry;
                return false;
            }
            await ProcessOutgoingPacketAsync( outgoingPacket, cancellationToken );
            while( true )
            {
                (outgoingPacket, timeUntilAnotherRetry) = await _localPacketStore.GetPacketToResendAsync();
                if( outgoingPacket is null )
                {
                    _timeUntilNextRetry = timeUntilAnotherRetry;
                    return true;
                }
                await ProcessOutgoingPacketAsync( outgoingPacket, cancellationToken );
            }
        }

        protected async ValueTask ProcessOutgoingPacketAsync( IOutgoingPacket outgoingPacket, CancellationToken cancellationToken )
        {
            if( cancellationToken.IsCancellationRequested ) return;
            if( outgoingPacket.Qos != QualityOfService.AtMostOnce )
            {
                // This must be done BEFORE writing the packet to avoid concurrency issues.
                if( !outgoingPacket.IsRemoteOwnedPacketId )
                {
                    _localPacketStore.OnPacketSent( outgoingPacket.PacketId );
                }
                // Explanation:
                // The receiver and input loop can run before the next line is executed.
            }
            await outgoingPacket.WriteAsync( _pConfig.ProtocolLevel, _pipeWriter, cancellationToken );
        }
    }
}
