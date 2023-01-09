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
        readonly ITimer _timer;
        protected readonly MessageExchanger MessageExchanger;
        public OutputProcessor( MessageExchanger messageExchanger )
        {
            MessageExchanger = messageExchanger;
            _timer = messageExchanger.Config.TimeUtilities.CreateTimer( TimerTimeoutWrapper );
        }

        void TimerTimeoutWrapper( object? obj ) => OnTimeout(Timeout.Infinite);

        protected ChannelReader<IOutgoingPacket> ReflexesChannel => MessageExchanger.OutputPump!.ReflexesChannel;
        protected ChannelReader<IOutgoingPacket> MessagesChannel => MessageExchanger.OutputPump!.MessagesChannel;

        public void Starting()
        {
            _timer.Change( GetTimeoutTime( Timeout.Infinite ), Timeout.Infinite );
        }

        public virtual async ValueTask<bool> SendPacketsAsync( CancellationToken cancellationToken )
        {
            bool newPacketSent = await SendAMessageFromQueueAsync( cancellationToken ); // We want to send a fresh new packet...
            (bool retriesSent, TimeSpan timeUntilNewAction) = await ResendAllUnackPacketAsync( cancellationToken ); // Then sending all packets that waited for too long.
            if( newPacketSent || retriesSent )
            {
                var timeout = GetTimeoutTime( (long)timeUntilNewAction.TotalMilliseconds );
                _timer.Change( timeout, Timeout.Infinite );
            }
            return newPacketSent || retriesSent;
        }

        protected virtual long GetTimeoutTime( long current ) => current;

        public virtual void OnTimeout( int msUntilNextTrigger )
        {
            _timer.Change( msUntilNextTrigger, Timeout.Infinite );
            MessageExchanger.OutputPump!.UnblockWriteLoop();
        }

        protected ValueTask SelfDisconnectAsync( DisconnectReason disconnectedReason ) => MessageExchanger.OutputPump!.SelfCloseAsync( disconnectedReason );

        protected virtual async ValueTask<bool> SendAMessageFromQueueAsync( CancellationToken cancellationToken )
        {
            IOutgoingPacket? packet;
            do
            {
                var outputPump = MessageExchanger.OutputPump!;
                if( !outputPump.ReflexesChannel.TryRead( out packet ) && !outputPump.MessagesChannel.TryRead( out packet ) )
                {
                    return false;
                }
            }
            while( packet == FlushPacket.Instance );
            await ProcessOutgoingPacketAsync( packet, cancellationToken );
            return true;
        }

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

        public ValueTask DisposeAsync() => _timer.DisposeAsync();
    }
}
