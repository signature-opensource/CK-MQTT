using System;
using System.Diagnostics;

#pragma warning disable RS0030 // Do not used banned APIs
namespace CK.MQTT
{
    class MqttStopwatch : IStopwatch
    {
        readonly Stopwatch _stopwatch;

        public MqttStopwatch( Stopwatch stopwatch )
        {
            _stopwatch = stopwatch;
        }

        public TimeSpan Elapsed => _stopwatch.Elapsed;

        public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

        public long ElapsedTicks => _stopwatch.ElapsedTicks;

        public bool IsRunning => _stopwatch.IsRunning;

        public void Reset() => _stopwatch.Reset();

        public void Restart() => _stopwatch.Restart();

        public void Start() => _stopwatch.Start();

        public void Stop() => _stopwatch.Stop();
    }
}
#pragma warning restore RS0030 // Do not used banned APIs
