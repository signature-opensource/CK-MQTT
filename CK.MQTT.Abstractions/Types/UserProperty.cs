using System;

namespace CK.MQTT
{
    public struct UserProperty
    {
        public string Name;
        public string Value;

        public UserProperty( string name, string value )
        {
            MqttBinaryWriter.ThrowIfInvalidMQTTString( name );
            MqttBinaryWriter.ThrowIfInvalidMQTTString( value );

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

        public override bool Equals( object obj )
            => obj is UserProperty u && u.Name == Name && u.Value == Value;

        public override int GetHashCode() => HashCode.Combine( Name, Value );


        public static bool operator ==( UserProperty left, UserProperty right ) => left.Equals( right );

        public static bool operator !=( UserProperty left, UserProperty right ) => !(left == right);
    }
}
