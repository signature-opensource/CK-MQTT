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
        public delegate IOutgoingPacket OutputTransformer( IActivityMonitor m, IOutgoingPacket outgoingPacket );
        readonly ChannelReader<IOutgoingPacket> _messageOut;
        readonly ChannelWriter<IOutgoingPacket> _messageIn;
        readonly ChannelWriter<IOutgoingPacket> _reflexIn;
        readonly ChannelReader<IOutgoingPacket> _reflexOut;
        readonly PipeWriter _pipeWriter;
        readonly Task _writeLoop;
        readonly CancellationTokenSource _dirtyStopSource = new CancellationTokenSource();
        public OutgoingMessageHandler( PipeWriter writer, Channel<IOutgoingPacket> messageChannel, Channel<IOutgoingPacket> reflexChannel )
        {
            _messageOut = messageChannel;
            _messageIn = messageChannel;
            _reflexIn = reflexChannel;
            _reflexOut = reflexChannel;
            _pipeWriter = writer;
            _writeLoop = WriteLoop();
        }

        public OutputTransformer? OutputMiddleware { get; set; }

        public bool QueueMessage( IOutgoingPacket item ) => _messageIn.TryWrite( item );

        public bool QueueReflexMessage( IOutgoingPacket item ) => _reflexIn.TryWrite( item );

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns>A <see cref="ValueTask"/> that complete when the packet is sent.</returns>
        public async ValueTask SendMessageAsync( IOutgoingPacket item )
        {
            var wrapper = new OutgoingPacketWrapper( item );
            await _messageIn.WriteAsync( wrapper );//ValueTask, will almost always return synchronously
            await wrapper.Sent;//TaskCompletionSource.Task, on some setup will often return synchronously, most of the time, asyncrounously.
        }

        async Task WriteLoop()
        {
            ActivityMonitor m = new ActivityMonitor();
            try
            {
                bool mainLoop = true;
                while( mainLoop )
                {
                    if( _reflexOut.TryRead( out IOutgoingPacket packet ) || _messageOut.TryRead( out packet ) )
                    {
                        await ProcessOutgoingPacket( m, packet );
                        continue;
                    }
                    mainLoop = await await Task.WhenAny( _reflexOut.WaitToReadAsync().AsTask(), _messageOut.WaitToReadAsync().AsTask() );
                }
                using( m.OpenTrace( "Sending remaining messages..." ) )
                {
                    while( _reflexOut.TryRead( out IOutgoingPacket packet ) ) await ProcessOutgoingPacket( m, packet );
                    while( _messageOut.TryRead( out IOutgoingPacket packet ) ) await ProcessOutgoingPacket( m, packet );
                }
                _pipeWriter.Complete();
            }
            catch( Exception e )
            {
                _stopEvent.Raise( m, this, e );
            }
            _stopEvent.Raise( m, this, null );
        }

        ValueTask ProcessOutgoingPacket( IActivityMonitor m, IOutgoingPacket outgoingPacket )
        {
            m.Info( $"Sending message of size {outgoingPacket.GetSize()}." );
            return (OutputMiddleware?.Invoke( m, outgoingPacket ) ?? outgoingPacket).WriteAsync( _pipeWriter, _dirtyStopSource.Token );
        }

        void DirtyStop()
        {
            _dirtyStopSource.Cancel();
            _pipeWriter.Complete();
            _pipeWriter.CancelPendingFlush();
        }

        public event SequentialEventHandler<OutgoingMessageHandler, Exception?> Stopped
        {
            add => _stopEvent.Add( value );
            remove => _stopEvent.Remove( value );
        }

        readonly SequentialEventHandlerSender<OutgoingMessageHandler, Exception?> _stopEvent = new SequentialEventHandlerSender<OutgoingMessageHandler, Exception?>();

        public Task Stop( CancellationToken dirtyStop )
        {
            dirtyStop.Register( () => DirtyStop() );
            if( !dirtyStop.IsCancellationRequested ) return _writeLoop;
            DirtyStop();
            return Task.CompletedTask;
        }
    }
}
