using CK.MQTT.Packets;
using CK.MQTT.Stores;
using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Pumps
{
    public class OutputProcessor : IAsyncDisposable
    {
        readonly Timer _timer;
        protected readonly MessageExchanger MessageExchanger;
        public OutputProcessor( MessageExchanger messageExchanger )
        {
            MessageExchanger = messageExchanger;
            _timer = new( TimerTimeoutWrapper );
        }

        void TimerTimeoutWrapper( object? obj ) => OnTimeout();

        protected Channel<IOutgoingPacket> ReflexesChannel => MessageExchanger.Pumps!.Left.ReflexesChannel;
        protected Channel<IOutgoingPacket> MessagesChannel => MessageExchanger.Pumps!.Left.MessagesChannel;


        [ThreadColor( "WriteLoop" )]
        public virtual async ValueTask<bool> SendPacketsAsync( CancellationToken cancellationToken )
        {
            bool newPacketSent = await SendAMessageFromQueueAsync( cancellationToken ); // We want to send a fresh new packet...
            (bool retriesSent, TimeSpan timeUntilNewAction) = await ResendAllUnackPacketAsync( cancellationToken ); // Then sending all packets that waited for too long.
            var timeout = GetTimeoutTime( (long)timeUntilNewAction.TotalMilliseconds );
            _timer.Change( timeout, Timeout.Infinite );
            return newPacketSent || retriesSent;
        }

        protected virtual long GetTimeoutTime( long current ) => current;

        public virtual void OnTimeout() => MessageExchanger.Pumps!.Left.UnblockWriteLoop();

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
        async ValueTask<(bool, TimeSpan)> ResendAllUnackPacketAsync( CancellationToken cancellationToken )
        {
            Debug.Assert( MessageExchanger.Config.WaitTimeoutMilliseconds != int.MaxValue );
            bool sentPacket = false;
            while( true )
            {
                (IOutgoingPacket? outgoingPacket, TimeSpan timeUntilNextRetry) = await MessageExchanger.LocalPacketStore.GetPacketToResendAsync();
                if( outgoingPacket is null )
                {
                    return (sentPacket, timeUntilNextRetry);
                }
                sentPacket = true;
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

        public ValueTask DisposeAsync()
        {
            return _timer.DisposeAsync();
        }
    }
}
