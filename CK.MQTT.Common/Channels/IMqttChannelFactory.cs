namespace CK.MQTT
{
    public interface IMqttChannelFactory
    {
        IMqttChannel Create( string connectionString );
    }
}
