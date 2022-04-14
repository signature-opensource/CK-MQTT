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
        readonly IStopwatch _stopwatch;
        readonly LowLevelMqttClient _client;

        public OutputProcessorWithKeepAlive( LowLevelMqttClient client )
            : base( client )
        {
            _stopwatch = client.Config.StopwatchFactory.Create();
            _client = client;
        }

        bool IsPingReqTimeout =>
            WaitingPingResp
            && MessageExchanger.Config.WaitTimeoutMilliseconds != int.MaxValue //We never timeout if it's configured to int.MaxValue.
            && _stopwatch.Elapsed.TotalMilliseconds > MessageExchanger.Config.WaitTimeoutMilliseconds;

        int TimeToWaitKeepAlive =>
            !WaitingPingResp ? _client.ClientConfig.KeepAliveSeconds * 1000
                : MessageExchanger.Config.WaitTimeoutMilliseconds - (int)_stopwatch.Elapsed.TotalMilliseconds;

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
            using( CancellationTokenSource cts = MessageExchanger.Config.CancellationTokenSourceFactory.Create( stopWaitToken, TimeToWaitKeepAlive ) )
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
