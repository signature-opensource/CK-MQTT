namespace CK.MQTT.P2P
{
    public class P2PMqttConfiguration : Mqtt3ClientConfiguration
    {
        public P2PMqttConfiguration() : base( "" )
        {
        }

        public IStoreFactory StoreFactory { get; init; } = new MemoryStoreFactory();
    }
}
