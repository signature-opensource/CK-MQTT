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
        readonly ITopicManager _topicManager;
        readonly ChannelReader<(Subscription[]?, string[]?)> _subscribes;
        public InterlacedInputPump
        (
            MessageExchanger messageExchanger, Reflex reflex, ITopicManager topicManager, ChannelReader<(Subscription[]?, string[]?)> subscribes
        ) : base( messageExchanger, reflex )
        {
            _topicManager = topicManager;
            _subscribes = subscribes;
        }

        protected override async ValueTask<ReadResult> ReadAsync( CancellationToken cancellationToken )
        {
            //Fast path
            var read = base.ReadAsync( cancellationToken );
            await ProcessSubscribesAsync();

            while( !read.IsCompleted )
            {
                //SlowPath
                Task readTask = read.AsTask();
                Task messageReady = _subscribes.WaitToReadAsync( cancellationToken ).AsTask();

                await Task.WhenAny( readTask, messageReady );
                if( messageReady.IsCompleted )
                {
                    await ProcessSubscribesAsync();
                }
            }
            return await read;
        }

        async ValueTask ProcessSubscribesAsync()
        {
            while( _subscribes.TryRead( out (Subscription[]?, string[]?) item ) )
            {
                if( item.Item1 is not null )
                {
                    foreach( var subscription in item.Item1 )
                    {
                        await _topicManager.SubscribeAsync( subscription );
                    }
                }
                else
                {
                    foreach( var topic in item.Item2! )
                    {
                        await _topicManager.UnsubscribeAsync( topic );
                    }
                }
            }
        }
    }
}
