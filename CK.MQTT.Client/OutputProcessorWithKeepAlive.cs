using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    class OutputProcessorWithKeepAlive : OutputProcessor, IReflexMiddleware
    {
        readonly MqttClientConfiguration _config;
        readonly IStopwatch _stopwatch;

        public OutputProcessorWithKeepAlive( ProtocolConfiguration pConfig, MqttClientConfiguration config, OutputPump outputPump, PipeWriter pipeWriter, IOutgoingPacketStore store )
            : base( pConfig, outputPump, pipeWriter, store )
        {
            _config = config;
            _stopwatch = config.StopwatchFactory.Create();
        }

        bool IsPingReqTimeout =>
            WaitingPingResp
            && _config.WaitTimeoutMilliseconds != int.MaxValue //We never timeout if it's configured to int.MaxValue.
            && _stopwatch.Elapsed.TotalMilliseconds > _config.WaitTimeoutMilliseconds;

        public bool WaitingPingResp { get; set; }

        public override async ValueTask<bool> SendPacketsAsync( IOutputLogger? m, CancellationToken cancellationToken )
        {
            if( IsPingReqTimeout )
            {
                await SelfDisconnectAsync( DisconnectedReason.PingReqTimeout );
                return true;
            }
            return await base.SendPacketsAsync( m, cancellationToken );
        }

        public override async Task WaitPacketAvailableToSendAsync( IOutputLogger? m, CancellationToken cancellationToken )
        {
            if( IsPingReqTimeout )
            {
                await SelfDisconnectAsync( DisconnectedReason.PingReqTimeout );
                return;
            }
            using( CancellationTokenSource cts = _config.CancellationTokenSourceFactory.Create( _config.KeepAliveSeconds * 1000 ) )
            using( cancellationToken.Register( () => cts.Cancel() ) )
            {
                await base.WaitPacketAvailableToSendAsync( m, cts.Token );
                // We didn't get cancelled, or the cancellation is due to the processor being cancelled.
                if( !cts.IsCancellationRequested || cancellationToken.IsCancellationRequested ) return;
            }
            using( m?.MainLoopSendingKeepAlive() )
            {
                await ProcessOutgoingPacketAsync( m, OutgoingPingReq.Instance, cancellationToken );
            }
            _stopwatch.Restart();
            WaitingPingResp = true;
        }

        public async ValueTask ProcessIncomingPacketAsync(
            IInputLogger? m,
            InputPump sender,
            byte header,
            int packetLength,
            PipeReader pipeReader,
            Func<ValueTask> next,
            CancellationToken cancellationToken )
        {
            if( PacketType.PingResponse != (PacketType)header )
            {
                await next();
                return;
            }
            using( m?.ProcessPacket( PacketType.PingResponse ) )
            {
                WaitingPingResp = false;
                if( packetLength > 0 ) m?.UnparsedExtraBytes( sender, PacketType.PingResponse, 0, packetLength, packetLength );
                await pipeReader.SkipBytesAsync( packetLength );
            }
        }
    }
}
