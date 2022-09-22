using System;

namespace CK.MQTT.Server.ServerClient
{
    public class MQTTServerClientConfiguration : MQTT3ConfigurationBase
    {
        public MQTTServerClientConfiguration()
        {
        }

        private DisconnectBehavior _disconnectBehavior = DisconnectBehavior.Nothing;
        public DisconnectBehavior DisconnectBehavior
        {
            get => _disconnectBehavior;
            init
            {
                if( !Enum.IsDefined( value ) ) throw new ArgumentOutOfRangeException( nameof( value ) );
                _disconnectBehavior = value;
            }
        }
    }
}
