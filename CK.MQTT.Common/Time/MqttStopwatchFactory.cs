using System.Diagnostics;

namespace CK.MQTT
{
    class MqttStopwatchFactory : IStopwatchFactory
    {
#pragma warning disable RS0030 // Do not used banned APIs
        public IStopwatch Create() => new MqttStopwatch( new Stopwatch() );
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
