using System.Diagnostics;

namespace CK.MQTT
{
    class StopwatchFactory : IStopwatchFactory
    {
#pragma warning disable RS0030 // Do not used banned APIs
        public IStopwatch Create() => new Stopwatch( new System.Diagnostics.Stopwatch() );
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
