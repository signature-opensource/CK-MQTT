using System;

namespace CK.MQTT
{
    public struct UserProperty
    {
        public string Name;
        public string Value;

        public UserProperty( string name, string value )
        {
            Name = name;
            Value = value;
        }

        public int Size => 1 + Name.MQTTSize() + Value.MQTTSize();

        public Span<byte> Write( Span<byte> buffer )
        {
            buffer[0] = (byte)PropertyIdentifier.UserProperty;
            return buffer[1..].WriteMQTTString( Name )
                .WriteMQTTString( Value );
        }

    }
}
