using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Client.Abstractions.Types
{
    class SimpleOutgoingApplicationMessage : OutgoingApplicationMessage
    {
        public SimpleOutgoingApplicationMessage( string topic, ReadOnlySequence<byte> payload )
            : base( topic, (int)payload.Length )
        {
            Payload = payload;
        }

        public ReadOnlySequence<byte> Payload { get; }
    }
}
