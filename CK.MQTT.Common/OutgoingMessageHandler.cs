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
        readonly Channel<IOutgoingPacket> _messages;
        readonly Channel<IOutgoingPacket> _reflexes;
        readonly PipeWriter _pipeWriter;
        readonly Task _writeLoop;
        readonly CancellationTokenSource _dirtyStopSource = new CancellationTokenSource();
        public OutgoingMessageHandler( PipeWriter writer, MqttConfiguration config )
        {
            _messages = Channel.CreateBounded<IOutgoingPacket>( config.ChannelsPacketCount );
            _reflexes = Channel.CreateBounded<IOutgoingPacket>( config.ChannelsPacketCount );
            _pipeWriter = writer;
            _writeLoop = WriteLoop();
        }

        public OutputTransformer? OutputMiddleware { get; set; }

        public bool QueueMessage( IOutgoingPacket item ) => _messages.Writer.TryWrite( item );

        public bool QueueReflexMessage( IOutgoingPacket item ) => _reflexes.Writer.TryWrite( item );

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns>A <see cref="ValueTask"/> that complete when the packet is sent.</returns>
        public async ValueTask SendMessageAsync( IOutgoingPacket item )
        {
            var wrapper = new OutgoingPacketWrapper( item );
            await _messages.Writer.WriteAsync( wrapper );//ValueTask, will almost always return synchronously
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
                    if( _reflexes.Reader.TryRead( out IOutgoingPacket packet ) || _messages.Reader.TryRead( out packet ) )
                    {
                        await ProcessOutgoingPacket( m, packet );
                        continue;
                    }
                    mainLoop = await await Task.WhenAny( _reflexes.Reader.WaitToReadAsync().AsTask(), _messages.Reader.WaitToReadAsync().AsTask() );
                }
                using( m.OpenTrace( "Sending remaining messages..." ) )
                {
                    while( _reflexes.Reader.TryRead( out IOutgoingPacket packet ) ) await ProcessOutgoingPacket( m, packet );
                    while( _messages.Reader.TryRead( out IOutgoingPacket packet ) ) await ProcessOutgoingPacket( m, packet );
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
