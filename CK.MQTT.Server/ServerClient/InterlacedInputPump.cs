using CK.MQTT.Client;
using CK.MQTT.Pumps;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.P2P
{
    class InterlacedInputPump : InputPump
    {
        readonly ITopicFilter _filter;
        readonly ChannelReader<(bool, string[])> _subscribes;
        public InterlacedInputPump
        (
            IMqtt3Sink sink,
            ITopicFilter filter,
            Func<DisconnectReason, ValueTask> onDisconnect, Mqtt3ConfigurationBase config, PipeReader pipeReader, Reflex reflex, ChannelReader<(bool, string[])> subscribes
        ) : base( sink, onDisconnect, config, pipeReader, reflex )
        {
            _filter = filter;
            _subscribes = subscribes;
        }

        protected override async ValueTask<ReadResult> ReadAsync( CancellationToken cancellationToken )
        {
            //Fast path
            var read = base.ReadAsync( cancellationToken );
            ProcessSubscribes();

            while( !read.IsCompleted )
            {
                //SlowPath

                Task readTask = read.AsTask();
                Task messageReady = _subscribes.WaitToReadAsync( cancellationToken ).AsTask();

                await Task.WhenAny( readTask, messageReady );
                if( messageReady.IsCompleted )
                {
                    ProcessSubscribes();
                }
            }
            return await read;

        }
        
        void ProcessSubscribes()
        {
            while( _subscribes.TryRead( out (bool, string[]) item ) )
            {
                if( item.Item1 )
                {
                    foreach( var topic in item.Item2 )
                    {
                        _filter.Subscribe( topic );
                    }
                }
                else
                {
                    foreach( var topic in item.Item2 )
                    {
                        _filter.Unsubscribe( topic );
                    }
                }
            }
        }
    }
}
