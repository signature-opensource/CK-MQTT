using System;

namespace CK.MQTT
{
    /// <summary>
    /// Configuration of a <see cref="ILowLevelMqtt3Client"/>.
    /// </summary>
    public class Mqtt5ClientConfiguration : Mqtt3ClientConfiguration
    {
        public override ProtocolConfiguration ProtocolConfiguration => ProtocolConfiguration.Mqtt5;
        public Mqtt5ClientConfiguration( string connectionString ) : base( connectionString )
        {
        }
    }
}
