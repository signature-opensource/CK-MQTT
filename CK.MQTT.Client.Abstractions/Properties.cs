namespace CK.MQTT
{
    public static class Properties
    {
        public static string ProtocolEncoding_IntegerMaxValueExceeded => "Integer values are expected to be of two bytes length. The max value supported is 65536";
        public static string ProtocolEncoding_MalformedRemainingLength => "Malformed Remaining Length";
        public static string ProtocolEncoding_StringMaxLengthExceeded => "String value cannot exceed 65536 bytes of length";
    }
}
