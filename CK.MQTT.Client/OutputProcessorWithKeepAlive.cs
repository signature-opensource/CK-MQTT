using CK.MQTT.Pumps;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    class OutputProcessorWithKeepAlive : OutputProcessor
    {
        readonly PingRespReflex _pingRespReflex;
        readonly MqttClientConfiguration _config;
        readonly IStopwatch _stopwatch;

        public OutputProcessorWithKeepAlive( PingRespReflex pingRespReflex, MqttClientConfiguration config, OutputPump outputPump, PipeWriter pipeWriter )
            : base( outputPump, pipeWriter )
        {
            _pingRespReflex = pingRespReflex;
            _config = config;
            _stopwatch = config.StopwatchFactory.Create();
        }

        bool IsPingReqTimeout =>
            _pingRespReflex.WaitingPingResp
            && _stopwatch.Elapsed.TotalMilliseconds > _config.WaitTimeoutMilliseconds
            && _config.WaitTimeoutMilliseconds != int.MaxValue; //We never timeout if it's configured to int.MaxValue.

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
            _pingRespReflex.WaitingPingResp = true;
        }
    }
}
