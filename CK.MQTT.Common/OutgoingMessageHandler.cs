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
        readonly Action<DisconnectedReason> _clientClose;
        readonly PipeWriter _pipeWriter;
        readonly Task _writeLoop;
        bool _stopped;
        readonly CancellationTokenSource _dirtyStopSource = new CancellationTokenSource();
        public OutgoingMessageHandler( Action<DisconnectedReason> clientClose, PipeWriter writer, MqttConfiguration config )
        {
            _messages = Channel.CreateBounded<IOutgoingPacket>( config.ChannelsPacketCount );
            _reflexes = Channel.CreateBounded<IOutgoingPacket>( config.ChannelsPacketCount );
            _clientClose = clientClose;
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
                Close( DisconnectedReason.UnspecifiedError );
                return;
            }
            Close( DisconnectedReason.SelfDisconnected );
        }

        ValueTask ProcessOutgoingPacket( IActivityMonitor m, IOutgoingPacket outgoingPacket )
        {
            if( _dirtyStopSource.IsCancellationRequested ) return new ValueTask();
            m.Info( $"Sending message of size {outgoingPacket.GetSize()}." );
            return (OutputMiddleware?.Invoke( m, outgoingPacket ) ?? outgoingPacket).WriteAsync( _pipeWriter, _dirtyStopSource.Token );
        }

        public void Close( DisconnectedReason disconnectedReason )
        {
            if( _stopped ) return;
            _stopped = true;
            _messages.Writer.Complete();
            _reflexes.Writer.Complete();
            _dirtyStopSource.Cancel();
            _pipeWriter.Complete();
            _clientClose( disconnectedReason );
        }
    }
}
