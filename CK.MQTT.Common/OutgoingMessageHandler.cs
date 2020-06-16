using CK.Core;
using CK.MQTT.Common.OutgoingPackets;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Channels
{
    public class OutgoingMessageHandler
    {
        public delegate OutgoingPacket OutputTransformer( IActivityMonitor m, OutgoingPacket outgoingPacket );

        readonly CancellationTokenSource _dirtyStopSource = new CancellationTokenSource();
        readonly ChannelReader<OutgoingPacket> _messageOut;
        readonly ChannelWriter<OutgoingPacket> _messageIn;
        readonly ChannelWriter<OutgoingPacket> _reflexIn;
        readonly ChannelReader<OutgoingPacket> _reflexOut;
        readonly OutputTransformer? _outputMiddleware;
        readonly CancellationToken _dirtyStop;
        readonly PipeWriter _pipeWriter;
        readonly Task _writeLoop;

        bool _stopping;
        public OutgoingMessageHandler(
            PipeWriter pipeWriter,
            OutputTransformer? outputMiddleware,
            Channel<OutgoingPacket> externalMessageChannel,
            Channel<OutgoingPacket> internalMessageChannel )
        {
            _dirtyStop = _dirtyStopSource.Token;
            _pipeWriter = pipeWriter;
            _outputMiddleware = outputMiddleware;
            _messageOut = externalMessageChannel;
            _messageIn = externalMessageChannel;
            _reflexIn = internalMessageChannel;
            _reflexOut = internalMessageChannel;
            _writeLoop = WriteLoop();
        }

        public bool QueueMessage( OutgoingPacket item ) => _messageIn.TryWrite( item );

        public bool QueueReflexMessage( OutgoingPacket item ) => _reflexIn.TryWrite( item );

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns>A <see cref="ValueTask"/> that complete when the packet is sent.</returns>
        public async ValueTask SendMessageAsync( OutgoingPacket item )
        {
            var wrapper = new OutgoingPacketWrapper( item );
            await _messageIn.WriteAsync( wrapper );//ValueTask, will almost always return synchronously
            await wrapper.Sent;//TaskCompletionSource.Task, on some machine will often return synchronously, most of the time, asyncrounously.
        }

        void FlushChannels()
        {
            while( _reflexOut.TryRead( out _ ) ) ; //We are just flushing the channel !
            while( _messageOut.TryRead( out _ ) ) ;
        }
        async Task WriteLoop()
        {
            ActivityMonitor m = new ActivityMonitor();
            try
            {

                while( true )
                {
                    if( _reflexOut.TryRead( out OutgoingPacket packet ) )
                    {
                        await ProcessOutgoingPacket( m, packet );
                        continue;
                    }
                    if( _messageOut.TryRead( out packet ) )
                    {
                        await ProcessOutgoingPacket( m, packet );
                        continue;
                    }

                    Task<bool> result = await Task.WhenAny(
                        _reflexOut.WaitToReadAsync( _dirtyStop ).AsTask(),
                        _messageOut.WaitToReadAsync( _dirtyStop ).AsTask()
                    );
                    if( !await result )
                    {
                        if( !_stopping ) throw new InvalidOperationException();
                        bool reflexDone = _reflexOut.Completion.IsCompleted;
                        bool messageDone = _messageOut.Completion.IsCompleted;
                        if( !reflexDone && !messageDone ) throw new InvalidOperationException();
                        //We are now sure we are in a normal stop.
                        ChannelReader<OutgoingPacket> channel = reflexDone ? _reflexOut : _messageOut;
                        while( channel.TryRead( out packet ) )
                        {
                            await ProcessOutgoingPacket( m, packet );
                        }
                        _pipeWriter.Complete();
                        return;
                    }
                }
            }
            catch( Exception e )
            {
                _pipeWriter.Complete( e );
            }
        }

        ValueTask ProcessOutgoingPacket( IActivityMonitor m, OutgoingPacket outgoingPacket ) =>
            (_outputMiddleware?.Invoke( m, outgoingPacket ) ?? outgoingPacket).WriteAsync( _pipeWriter, _dirtyStop );

        public Task Stop( CancellationToken dirtyStop )
        {
            dirtyStop.Register( () =>
            {
                FlushChannels();
                _dirtyStopSource.Cancel();
            } );
            if( dirtyStop.IsCancellationRequested )
            {
                FlushChannels();
                _dirtyStopSource.Cancel();
            }
            _stopping = true;
            _messageIn.Complete();
            _reflexIn.Complete();
            return Task.WhenAll( _messageOut.Completion, _reflexOut.Completion, _writeLoop );
        }
    }
}
