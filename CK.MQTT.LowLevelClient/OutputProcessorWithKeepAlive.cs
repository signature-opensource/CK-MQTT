using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    class OutputProcessorWithKeepAlive : OutputProcessor, IReflexMiddleware
    {
        readonly MQTT3ClientConfiguration _config;
        readonly Func<DisconnectReason, ValueTask> _closeHandler;
        DateTime _lastPacketSent;

        public OutputProcessorWithKeepAlive( PipeWriter pipeWriter, ProtocolConfiguration pConfig, MQTT3ClientConfiguration config, ILocalPacketStore localPacketStore, Func<DisconnectReason, ValueTask> closeHandler )
            : base(pipeWriter, pConfig, config, localPacketStore )
        {
            _config = config;
            _closeHandler = closeHandler;
            _lastPacketSent = config.TimeUtilities.UtcNow;
        }

        public bool WaitingPingResp { get; set; }

        public override async ValueTask<bool> SendPacketsAsync( CancellationToken cancellationToken )
        {
            if( WaitingPingResp )
            {
                var now = _config.TimeUtilities.UtcNow;
                var elapsed = now - _lastPacketSent;

                if( elapsed.TotalMilliseconds > _config.WaitTimeoutMilliseconds )
                {
                    await _closeHandler( DisconnectReason.Timeout );
                    return true;
                }
            }
            var res = await base.SendPacketsAsync( cancellationToken );
            if( res )
            {
                _lastPacketSent = _config.TimeUtilities.UtcNow;
            }
            return res;
        }


        protected override long GetTimeoutTime( long current )
        {
            var sendTime = _lastPacketSent.AddSeconds( _config.KeepAliveSeconds );
            var timeUntilTimeout = sendTime - _config.TimeUtilities.UtcNow;
            var inMsFloored = Math.Max( (int)timeUntilTimeout.TotalMilliseconds, 0 );
            var baseVal = base.GetTimeoutTime( current );
            if( baseVal == Timeout.Infinite ) return inMsFloored;
            return Math.Min( baseVal, inMsFloored );
        }

        public override void OnTimeout( int msUntilNextTrigger )
        {
            // These 2 lines must be set before ProcessOutgoingPacketAsync to avoid a race condition.
            // ProcessIncomingPacketAsync can be processed before any other line is executed, therefore, these 2 lines modifying the state must be ran before.

            // There can be a concurrency "bug" if the server send an unexpected PingResp while we were about to send a PingReq.
            // Two thing can happen because WaitingResp is concurrently set:
            // 1. WaitingPingResp = true is getting erased with false, in this case we can consider that this unexpected PingResp is our response.
            // 2. WaitingPingResp = false is getting erased with true, in this case, we consider the unexpected PingResp is, unexpected,
            //                      and we expect the server to send another one.
            if(WaitingPingResp)
            {
                base.OnTimeout(Timeout.Infinite);
                return;
            }
            WaitingPingResp = true;
            _lastPacketSent =_config.TimeUtilities.UtcNow;
            OutputPump.TryQueueMessage( OutgoingPingReq.Instance );
            base.OnTimeout( _config.WaitTimeoutMilliseconds );
        }

        public async ValueTask<(OperationStatus, bool)> ProcessIncomingPacketAsync(
            IMQTT3Sink sink,
            InputPump sender,
            byte header,
            uint packetLength,
            PipeReader pipeReader,
            CancellationToken cancellationToken )
        {
            if( PacketType.PingResponse != (PacketType)header ) return (OperationStatus.Done, false);
            WaitingPingResp = false;
            await pipeReader.UnparsedExtraDataAsync( sink, 0, packetLength, cancellationToken );
            return (OperationStatus.Done, true);
        }
    }
}
