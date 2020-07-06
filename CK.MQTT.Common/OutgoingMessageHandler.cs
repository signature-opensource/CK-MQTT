using CK.Core;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class OutgoingMessageHandler
    {
        public delegate IOutgoingPacket OutputTransformer( IMqttLogger m, IOutgoingPacket outgoingPacket );
        readonly Channel<IOutgoingPacket> _messages;
        readonly Channel<IOutgoingPacket> _reflexes;
        readonly Action<IMqttLogger, DisconnectedReason> _clientClose;
        readonly PipeWriter _pipeWriter;
        readonly Task _writeLoop;
        bool _stopped;
        readonly CancellationTokenSource _dirtyStopSource = new CancellationTokenSource();
        public OutgoingMessageHandler( IMqttLoggerFactory loggerFactory, Action<IMqttLogger, DisconnectedReason> clientClose, PipeWriter writer, MqttConfiguration config )
        {
            _messages = Channel.CreateBounded<IOutgoingPacket>( config.ChannelsPacketCount );
            _reflexes = Channel.CreateBounded<IOutgoingPacket>( config.ChannelsPacketCount );
            _clientClose = clientClose;
            _pipeWriter = writer;
            _writeLoop = WriteLoop( loggerFactory );
        }

        public OutputTransformer? OutputMiddleware { get; set; }

        public bool QueueMessage( IOutgoingPacket item ) => _messages.Writer.TryWrite( item );

        public bool QueueReflexMessage( IOutgoingPacket item ) => _reflexes.Writer.TryWrite( item );

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns>A <see cref="ValueTask"/> that complete when the packet is sent.</returns>
        public async ValueTask<Task> SendMessageAsync( IOutgoingPacket item )
        {
            var wrapper = new OutgoingPacketWrapper( item );
            await _messages.Writer.WriteAsync( wrapper );//ValueTask, will almost always return synchronously
            return wrapper.Sent;//TaskCompletionSource.Task, on some setup will often return synchronously, most of the time, asyncrounously.
        }

        async Task WriteLoop( IMqttLoggerFactory loggerFactory )
        {
            IMqttLogger m = loggerFactory.Create();
            try
            {
                bool mainLoop = true;
                while( mainLoop )
                {
                    if( _dirtyStopSource.IsCancellationRequested ) break;
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
            }
            catch( Exception e )
            {
                m.Error( e );
                Close( m, DisconnectedReason.UnspecifiedError );
                return;
            }
            Close( m, DisconnectedReason.SelfDisconnected );
        }

        ValueTask<bool> ProcessOutgoingPacket( IMqttLogger m, IOutgoingPacket outgoingPacket )
        {
            if( _dirtyStopSource.IsCancellationRequested ) return new ValueTask<bool>( false );
            m.Info( $"Sending message of size {outgoingPacket.Size}." );
            return (OutputMiddleware?.Invoke( m, outgoingPacket ) ?? outgoingPacket).WriteAsync( _pipeWriter, _dirtyStopSource.Token );
        }

        bool _complete;
        public Task Complete()
        {
            if( _complete ) return _writeLoop;
            _complete = true;
            _messages.Writer.Complete();
            _reflexes.Writer.Complete();
            return _writeLoop;
        }

        public void Close( IMqttLogger m, DisconnectedReason disconnectedReason )
        {
            if( _stopped ) return;
            _stopped = true;
            var task = Complete();
            _dirtyStopSource.Cancel();
            _pipeWriter.Complete();
            if( !task.IsCompleted )
            {
                m.Warn( "OutgoingMessageHandler main loop is taking time to exit..." );
                task.Wait();
            }
            _clientClose( m, disconnectedReason );
        }
    }
}
