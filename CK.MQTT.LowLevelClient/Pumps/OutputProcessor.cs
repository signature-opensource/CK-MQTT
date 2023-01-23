using CK.MQTT.LowLevelClient.Time;
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
    public class OutputProcessor : IDisposable
    {
        readonly ITimer _timer;
        readonly PipeWriter _pipeWriter;
        readonly ProtocolConfiguration _pConfig;
        readonly MQTT3ConfigurationBase _config;
        readonly ILocalPacketStore _localPacketStore;

        public OutputProcessor( PipeWriter pipeWriter, ProtocolConfiguration pConfig, MQTT3ConfigurationBase config, ILocalPacketStore localPacketStore )
        {
            _timer = config.TimeUtilities.CreateTimer( TimerTimeoutWrapper );
            _pipeWriter = pipeWriter;
            _pConfig = pConfig;
            _config = config;
            _localPacketStore = localPacketStore;
        }

        public OutputPump OutputPump { get; set; } = null!;

        void TimerTimeoutWrapper( object? obj ) => OnTimeout( Timeout.Infinite );

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
            OutputPump.UnblockWriteLoop();
        }

        protected virtual async ValueTask<bool> SendAMessageFromQueueAsync( CancellationToken cancellationToken )
        {
            IOutgoingPacket? packet;
            do
            {
                var outputPump = OutputPump;
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
            Debug.Assert( _config.WaitTimeoutMilliseconds != int.MaxValue );
            bool sentPacket = false;
            while( true )
            {
                (IOutgoingPacket? outgoingPacket, TimeSpan timeUntilNextRetry) = await _localPacketStore.GetPacketToResendAsync();
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
                    _localPacketStore.OnPacketSent( outgoingPacket.PacketId );
                }
                // Explanation:
                // The receiver and input loop can run before the next line is executed.
            }
            await outgoingPacket.WriteAsync( _pConfig.ProtocolLevel, _pipeWriter, cancellationToken );
        }

        public void Dispose() => _timer.Dispose();
    }
}
