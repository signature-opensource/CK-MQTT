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
        readonly Mqtt3ClientConfiguration _config;
        readonly IStopwatch _stopwatch;

        public OutputProcessorWithKeepAlive( Mqtt3ClientConfiguration config, OutputPump outputPump, PipeWriter pipeWriter, ILocalPacketStore store, IRemotePacketStore remotePacketStore )
            : base( config.ProtocolConfiguration, outputPump, pipeWriter, store )
        {
            _config = config;
            _stopwatch = config.StopwatchFactory.Create();
        }

        bool IsPingReqTimeout =>
            WaitingPingResp
            && _config.WaitTimeoutMilliseconds != int.MaxValue //We never timeout if it's configured to int.MaxValue.
            && _stopwatch.Elapsed.TotalMilliseconds > _config.WaitTimeoutMilliseconds;

        int TimeToWaitKeepAlive =>
            !WaitingPingResp ? _config.KeepAliveSeconds * 1000
                : _config.WaitTimeoutMilliseconds - (int)_stopwatch.Elapsed.TotalMilliseconds;

        public bool WaitingPingResp { get; set; }

        public override async ValueTask<bool> SendPacketsAsync( CancellationToken cancellationToken )
        {
            if( IsPingReqTimeout )
            {
                await SelfDisconnectAsync( DisconnectReason.PingReqTimeout );
                return true;
            }
            return await base.SendPacketsAsync( cancellationToken );
        }

        public override async Task WaitPacketAvailableToSendAsync( CancellationToken stopWaitToken, CancellationToken stopToken )
        {
            if( IsPingReqTimeout )
            {
                await SelfDisconnectAsync( DisconnectReason.PingReqTimeout );
                return;
            }
            using( CancellationTokenSource cts = _config.CancellationTokenSourceFactory.Create( stopWaitToken, TimeToWaitKeepAlive ) )
            {
                await base.WaitPacketAvailableToSendAsync( cts.Token, stopToken );
                // We didn't get cancelled, or the cancellation is due to the processor being cancelled.
                if( stopWaitToken.IsCancellationRequested || stopToken.IsCancellationRequested ) return;
            }
            if( WaitingPingResp )
            {
                await SelfDisconnectAsync( DisconnectReason.PingReqTimeout );
                return;
            }

            // These 2 lines must be set before ProcessOutgoingPacketAsync to avoid a race condition.
            // ProcessIncomingPacketAsync can be processed before any other line is executed, therefore, these 2 lines modifying the state must be ran before.

            // There can be a concurrency "bug" if the server send an unexpected PingResp while we were about to send a PingReq.
            // Two thing can happen because WaitingResp is concurrently set:
            // 1. WaitingPingResp = true is getting erased with false, in this case we can consider that this unexpected PingResp is our response.
            // 2. WaitingPingResp = false is getting erased with true, in this case, we consider the unexpected PingResp is, unexpected,
            //                      and we expect the server to send another one.

            WaitingPingResp = true;
            _stopwatch.Restart(); 
            await ProcessOutgoingPacketAsync( OutgoingPingReq.Instance, stopToken );
        }

        public async ValueTask<OperationStatus> ProcessIncomingPacketAsync(
            IMqtt3Sink sink,
            InputPump sender,
            byte header,
            uint packetLength,
            PipeReader pipeReader,
            Func<ValueTask<OperationStatus>> next,
            CancellationToken cancellationToken )
        {
            if( PacketType.PingResponse != (PacketType)header )
            {
                return await next();
            }
            WaitingPingResp = false;
            await pipeReader.SkipBytesAsync( sink, 0, packetLength, cancellationToken );
            return OperationStatus.Done;
        }
    }
}
