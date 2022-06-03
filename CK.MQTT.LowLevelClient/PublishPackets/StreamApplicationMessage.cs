using CK.MQTT.Packets;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.LowLevelClient.PublishPackets
{
    public class StreamApplicationMessage : OutgoingMessage
    {
        readonly Stream _stream;
        readonly uint _length;
        readonly bool _leaveOpen;

        public StreamApplicationMessage(
            Stream knowLengthStream,
            bool leaveOpen,
            string topic, QualityOfService qos, bool retain, string? responseTopic = null, ushort correlationDataSize = 0, SpanAction? correlationDataWriter = null ) : base( topic, qos, retain, responseTopic, correlationDataSize, correlationDataWriter )
        {
            if( knowLengthStream.Length > 268_435_455 ) throw new ArgumentException( "The buffer exceeed the max allowed size.", nameof( knowLengthStream ) );
            _stream = knowLengthStream;
            _length = (uint)knowLengthStream.Length;
            _leaveOpen = leaveOpen;
        }

        protected override uint PayloadSize => _length;

        protected override async ValueTask WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            await _stream.CopyToAsync( pw, cancellationToken );
            if( !_leaveOpen ) await _stream.DisposeAsync();
        }
    }
}
