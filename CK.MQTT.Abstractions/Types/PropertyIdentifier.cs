namespace CK.MQTT
{
    public enum PropertyIdentifier : byte //Specs says "this is a variable byte integer but for now only one byte is used", one byte simplify implementation, so this enum is a byte for now.
    {
        PayloadFormatIndicator = 0x01,
        MessageExpiryInterval = 0x02,
        ContentType = 0x03,
        ResponseTopic = 0x08,
        CorrelationData = 0x09,
        SubscriptionIdentifier = 0x0B,
        SessionExpiryInterval = 0x11,
        AssignedClientIdentifier = 0x12,
        ServerKeepAlive = 0x13,
        AuthenticationMethod = 0x15,
        AuthenticationData = 0x16,
        RequestProblemInformation = 0x17,
        WillDelayInterval = 0x18,
        RequestResponseInformation = 0x19,
        ResponseInformation = 0x1A,
        ServerReference = 0x1C,
        ReasonString = 0x1F,
        ReceiveMaximum = 0x21,
        TopicAliasMaximum = 0x22,
        TopicAlias = 0x23,
        MaximumQoS = 0x24,
        RetainAvailable = 0x25,
        UserProperty = 0x26,
        MaximumPacketSize = 0x27,
        WildcardSubscriptionAvailable = 0x28,
        SubscriptionIdentifierAvailable = 0x29,
        SharedSubscriptionAvailable = 0x2A
    }
}
