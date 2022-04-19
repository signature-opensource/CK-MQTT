using CK.MQTT.Packets;
using CK.MQTT.Stores;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Pumps
{
    public class OutputProcessor
    {
        protected readonly MessageExchanger MessageExchanger;

        public OutputProcessor( MessageExchanger messageExchanger )
        {
            MessageExchanger = messageExchanger;
        }

        protected Channel<IOutgoingPacket> ReflexesChannel => MessageExchanger.Pumps!.Left.ReflexesChannel;
        protected Channel<IOutgoingPacket> MessagesChannel => MessageExchanger.Pumps!.Left.MessagesChannel;

        TimeSpan _timeUntilNextRetry = TimeSpan.MaxValue;

        [ThreadColor( "WriteLoop" )]
        public virtual async ValueTask<bool> SendPacketsAsync( CancellationToken cancellationToken )
        {
            bool newPacketSent = await SendAMessageFromQueueAsync( cancellationToken ); // We want to send a fresh new packet...
            bool retriesSent = await ResendAllUnackPacketAsync( cancellationToken ); // Then sending all packets that waited for too long.
            return newPacketSent || retriesSent;
        }

        public virtual async Task WaitPacketAvailableToSendAsync( CancellationToken stopWaitToken, CancellationToken stopToken )
        {
            // If we wait for too long, we may miss things like sending a keepalive, so we need to compute the minimal amount of time we have to wait.
            CancellationToken cancelOnPacketDropped = MessageExchanger.LocalPacketStore.DroppedPacketCancelToken;
            var outputPump = MessageExchanger.Pumps!.Left;
            using( var ctsRetry = MessageExchanger.Config.CancellationTokenSourceFactory.Create( _timeUntilNextRetry ) )
            using( var linkedCts = CancellationTokenSource.CreateLinkedTokenSource( ctsRetry.Token, cancelOnPacketDropped, stopWaitToken ) )
            using( linkedCts.Token.Register( () => outputPump.ReflexesChannel.Writer.TryWrite( FlushPacket.Instance ) ) )
            {
                await outputPump.ReflexesChannel.Reader.WaitToReadAsync( stopToken );
            }
        }

        protected ValueTask SelfDisconnectAsync( DisconnectReason disconnectedReason ) => MessageExchanger.Pumps!.Left.SelfCloseAsync( disconnectedReason );

        [ThreadColor( "WriteLoop" )]
        protected virtual async ValueTask<bool> SendAMessageFromQueueAsync( CancellationToken cancellationToken )
        {
            IOutgoingPacket? packet;
            do
            {
                var outputPump = MessageExchanger.Pumps!.Left;
                if( !outputPump.ReflexesChannel.Reader.TryRead( out packet ) && !outputPump.MessagesChannel.Reader.TryRead( out packet ) )
                {
                    return false;
                }
            }
            while( packet == FlushPacket.Instance );
            await ProcessOutgoingPacketAsync( packet, cancellationToken );
            return true;
        }

        [ThreadColor( "WriteLoop" )]
        async ValueTask<bool> ResendAllUnackPacketAsync( CancellationToken cancellationToken )
        {
            if( MessageExchanger.Config.WaitTimeoutMilliseconds == int.MaxValue ) return false; // Resend is disabled.

            (IOutgoingPacket? outgoingPacket, TimeSpan timeUntilAnotherRetry) = await MessageExchanger.LocalPacketStore.GetPacketToResendAsync();
            if( outgoingPacket is null )
            {
                _timeUntilNextRetry = timeUntilAnotherRetry;
                return false;
            }
            await ProcessOutgoingPacketAsync( outgoingPacket, cancellationToken );
            while( true )
            {
                (outgoingPacket, timeUntilAnotherRetry) = await MessageExchanger.LocalPacketStore.GetPacketToResendAsync();
                if( outgoingPacket is null )
                {
                    _timeUntilNextRetry = timeUntilAnotherRetry;
                    return true;
                }
                await ProcessOutgoingPacketAsync( outgoingPacket, cancellationToken );
            }
        }

        [ThreadColor( "WriteLoop" )]
        protected async ValueTask ProcessOutgoingPacketAsync( IOutgoingPacket outgoingPacket, CancellationToken cancellationToken )
        {
            if( cancellationToken.IsCancellationRequested ) return;
            if( outgoingPacket.Qos != QualityOfService.AtMostOnce )
            {
                // This must be done BEFORE writing the packet to avoid concurrency issues.
                if( !outgoingPacket.IsRemoteOwnedPacketId )
                {
                    MessageExchanger.LocalPacketStore.OnPacketSent( outgoingPacket.PacketId );
                }
                // Explanation:
                // The receiver and input loop can run before the next line is executed.
            }
            await outgoingPacket.WriteAsync( MessageExchanger.PConfig.ProtocolLevel, MessageExchanger.Channel.DuplexPipe!.Output, cancellationToken );
        }
    }
}
