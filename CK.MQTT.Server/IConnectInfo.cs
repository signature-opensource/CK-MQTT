using System;
using System.Collections.Generic;

namespace CK.MQTT.Server
{
    public interface IConnectInfo
    {
        public bool HasUserName { get; }
        public bool HasPassword { get; }
        public bool Retain { get; }
        public QualityOfService QoS { get; }
        public bool HasLastWill { get; }
        public bool CleanSession { get; }
        public IReadOnlyList<(string, string)> UserProperties { get; }
        public uint MaxPacketSize { get; }
        public uint SessionExpiryInterval { get; }
        public ushort ReceiveMaximum { get; }
        public ushort TopicAliasMaximum { get; }
        public bool RequestResponseInformation { get; }
        public bool RequestProblemInformation { get; }
        public string? AuthenticationMethod { get; }
        public ReadOnlyMemory<byte> AuthData { get; }
        public string ClientId { get; }

        public ushort KeepAlive { get; }
        public string ProtocolName { get; }
        public ProtocolLevel ProtocolLevel { get; }
        public string? UserName { get; }
        public string? Password { get; }
    }
}
