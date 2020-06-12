using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;

namespace CK.MQTT.Common.Processes
{
    public static class PingWriter
    {
        public static void WriteResponse( PipeWriter writer )
        {
            Span<byte> buffer = writer.GetSpan( 2 );
            buffer[0] = (byte)PacketType.PingRequest;
            buffer[1] = 0;//Length is 0
            writer.Advance( 2 );
        }

        public static void WriteRequest(PipeWriter writer)
        {
            Span<byte> buffer = writer.GetSpan( 2 );
            buffer[0] = (byte)PacketType.PingResponse;
            buffer[1] = 0;//Length is 0
            writer.Advance( 2 );
        }
    }
}
