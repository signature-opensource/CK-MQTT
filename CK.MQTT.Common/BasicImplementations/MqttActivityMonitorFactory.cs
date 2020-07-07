using CK.Core;

namespace CK.MQTT
{
    public class MqttActivityMonitorFactory : IMqttLoggerFactory
    {
        public IMqttLogger Create() => new MqttActivityMonitor( new ActivityMonitor() );
    }
}
