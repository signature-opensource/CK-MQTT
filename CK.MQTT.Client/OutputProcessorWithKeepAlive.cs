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

        public OutputProcessorWithKeepAlive( MqttClientConfiguration config, OutputPump outputPump, PipeWriter pipeWriter, IOutgoingPacketStore store )
            : base( outputPump, pipeWriter, store )
        {
            _config = config;
            _stopwatch = config.StopwatchFactory.Create();
        }

        bool IsPingReqTimeout =>
            WaitingPingResp
            && _config.WaitTimeoutMilliseconds != int.MaxValue //We never timeout if it's configured to int.MaxValue.
            && _stopwatch.Elapsed.TotalMilliseconds > _config.WaitTimeoutMilliseconds;

        public bool WaitingPingResp { get; set; }

        public override async ValueTask<bool> SendPackets( IOutputLogger? m, CancellationToken cancellationToken )
        {
            if( IsPingReqTimeout ) // Because we are in a loop, this will be called immediately after a return. Keep this in mind.
            {
                await OutputPump.DisconnectAsync( DisconnectedReason.PingReqTimeout );
                return true; // true so that the loop exit immediatly without calling the next method.
            }
            return await base.SendPackets( m, cancellationToken );
        }

        public override async Task WaitPacketAvailableToSendAsync( IOutputLogger? m, CancellationToken cancellationToken )
        {
            if( IsPingReqTimeout ) // Because we are in a loop, this will be called immediately after a return. Keep this in mind.
            {
                await OutputPump.DisconnectAsync( DisconnectedReason.PingReqTimeout );
                return;
            }
            Task packetAvailable = base.WaitPacketAvailableToSendAsync( m, cancellationToken );
            Task keepAlive = _config.DelayHandler.Delay( _config.KeepAliveSeconds * 1000, cancellationToken );
            _ = await Task.WhenAny( packetAvailable, keepAlive );
            if( packetAvailable.IsCompleted ) return;
            using( m?.MainLoopSendingKeepAlive() )
            {
                await ProcessOutgoingPacket( m, OutgoingPingReq.Instance, cancellationToken );
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
                await pipeReader.SkipBytes( packetLength );
            }
        }
    }
}
