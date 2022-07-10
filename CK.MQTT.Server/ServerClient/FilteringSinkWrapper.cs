using CK.MQTT.Client;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server.ServerClient
{
    class FilteringSinkWrapper : Mqtt3SinkWrapper
    {
        readonly IMqtt3Sink _sink;
        readonly ITopicFilter _topicFilter;
        public FilteringSinkWrapper( IMqtt3Sink sink, ITopicFilter topicFilter ) : base( sink )
        {
            _sink = sink;
            _topicFilter = topicFilter;
        }

        public async ValueTask ReceiveAsync( string topic,
                                            PipeReader reader,
                                            uint size,
                                            QualityOfService q,
                                            bool retain,
                                            CancellationToken cancellationToken )
        {
            if( _topicFilter.IsFiltered( topic ) )
            {
                await reader.SkipBytesAsync( _sink, 0, size, cancellationToken );
                return;
            }
            await _sink.ReceiveAsync( topic, reader, size, q, retain, cancellationToken );
        }
    }
}
