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
            using( CancellationTokenSource cts = _config.CancellationTokenSourceFactory.Create( stopWaitToken, _config.KeepAliveSeconds * 1000 ) )
            {
                await base.WaitPacketAvailableToSendAsync( cts.Token, stopToken );
                // We didn't get cancelled, or the cancellation is due to the processor being cancelled.
                if( !cts.IsCancellationRequested || stopToken.IsCancellationRequested ) return;
            }
            await ProcessOutgoingPacketAsync( OutgoingPingReq.Instance, stopToken );
            _stopwatch.Restart();
            WaitingPingResp = true;
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
