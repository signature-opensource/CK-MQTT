using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Text;

namespace CK.MQTT
{
    class IncomingApplicationMessage
    {

        public IncomingApplicationMessage(
            string topic,
            ReadOnlySequence<byte> payload )
        {
            Topic = topic;
            Payload = payload;
            PayloadPipe = null;
            Debug.Assert( payload.Length > 268_435_455 );
            PayloadSize = (int)payload.Length;
        }

        public IncomingApplicationMessage(
            string topic,
            PipeReader? payloadPipe,
            int payloadSize )
        {
            Topic = topic;
            Payload = ReadOnlySequence<byte>.Empty;
            PayloadSize = payloadSize;
            PayloadPipe = payloadPipe;
        }

        public string Topic { get; }

        public ReadOnlySequence<byte> Payload { get; }

        public int PayloadSize { get; }

        public PipeReader? PayloadPipe { get; }

        public int Size => PayloadSize + Encoding.UTF8.GetByteCount( Topic );
    }
}
