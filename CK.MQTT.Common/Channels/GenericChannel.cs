using CK.Core;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Channels
{
    public class GenericChannel : IMqttChannel<IPacket>
    {
        readonly IGenericChannelStream _channelStream;
        ValueTask? _backgroundListening;
        readonly CancellationToken _cancellationToken;
        readonly CancellationTokenSource _cancellationTokenSource;
        bool _running;
        GenericChannel(
            IGenericChannelStream channelStream )
        {
            _channelStream = channelStream;
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
        }

        public static GenericChannel Create( IGenericChannelStream channelStream, Memory<byte> workBuffer )
        {
            var channel = new GenericChannel( channelStream );
            channel.Start( workBuffer );
            return channel;
        }
        void Start( Memory<byte> workBuffer )
        {
            _running = true;
            _backgroundListening = PullToPush( workBuffer );
        }

        async ValueTask PullToPush( Memory<byte> workBuffer )
        {
            IActivityMonitor m = new ActivityMonitor( "Background Channel Listener." );
            int byteAlreadyReadCount = 0;
            while( _running && !_cancellationToken.IsCancellationRequested )
            {
                (IPacket? packet, ReadOnlyMemory<byte> buffer, IDisposable? memoryHandle) =
                    await PacketStreamParser.ReadPacket( m, workBuffer, byteAlreadyReadCount, _channelStream, _cancellationToken );
                if( packet == null )
                {
                    m.Error( "Error while reading incoming packet." );
                    break;
                }
                await RaiseReceivedAsync( m, packet );
                memoryHandle?.Dispose();
                buffer.CopyTo( workBuffer );//move what we have read at the beginning of the buffer.
                byteAlreadyReadCount = buffer.Length;
            }
            m.MonitorEnd();
        }

        public bool IsConnected => _channelStream.IsConnected;

        readonly SequentialEventHandlerSender<IMqttChannel<IPacket>, IPacket> _eSeqReceived = new SequentialEventHandlerSender<IMqttChannel<IPacket>, IPacket>();

        public event SequentialEventHandler<IMqttChannel<IPacket>, IPacket> Received
        {
            add => _eSeqReceived.Add( value );
            remove => _eSeqReceived.Remove( value );
        }

        readonly ParallelEventHandlerAsyncSender<IMqttChannel<IPacket>, IPacket> _eParReceivedAsync = new ParallelEventHandlerAsyncSender<IMqttChannel<IPacket>, IPacket>();

        public event ParallelEventHandlerAsync<IMqttChannel<IPacket>, IPacket> ParallelReceivedAsync
        {
            add => _eParReceivedAsync.Add( value );
            remove => _eParReceivedAsync.Remove( value );
        }

        readonly SequentialEventHandlerAsyncSender<IMqttChannel<IPacket>, IPacket> _eSeqReceivedAsync = new SequentialEventHandlerAsyncSender<IMqttChannel<IPacket>, IPacket>();

        public event SequentialEventHandlerAsync<IMqttChannel<IPacket>, IPacket> ReceivedAsync
        {
            add => _eSeqReceivedAsync.Add( value );
            remove => _eSeqReceivedAsync.Remove( value );
        }

        Task RaiseReceivedAsync( IActivityMonitor m, IPacket packet )
        {
            Task task = _eParReceivedAsync.RaiseAsync( m, this, packet );
            _eSeqReceived.Raise( m, this, packet );
            return Task.WhenAll( task, _eSeqReceivedAsync.RaiseAsync( m, this, packet ) );
        }

        readonly SequentialEventHandlerSender<IMqttChannel<IPacket>, IPacket> _sent = new SequentialEventHandlerSender<IMqttChannel<IPacket>, IPacket>();

        public event SequentialEventHandler<IMqttChannel<IPacket>, IPacket> Sent
        {
            add => _eSeqReceived.Add( value );
            remove => _eSeqReceived.Remove( value );
        }

        public async ValueTask CloseAsync( IActivityMonitor m, CancellationToken cancellationToken )
        {
            if( !_running || _cancellationTokenSource.IsCancellationRequested ) return;
            using( m.OpenTrace( "Closing Channel..." ) )
            {
                _running = false;
                cancellationToken.Register( () => _cancellationTokenSource.Cancel() );
                await _backgroundListening!.Value;
                _channelStream.Close();
            }
        }

        public void Dispose() => _channelStream.Dispose();

        public async ValueTask SendAsync( IActivityMonitor m, IPacket message, CancellationToken cancellationToken )
        {
            uint remainingLength = message.RemainingLength;
            byte lengthSize = MqttBinaryWriter.ComputeRemainingLengthSize( remainingLength );
            int size = lengthSize + (1 + (int)remainingLength);
            using( IMemoryOwner<byte> rentedMemory = MemoryPool<byte>.Shared.Rent( size ) )
            {
                Memory<byte> memory = rentedMemory.Memory[..size];
                memory.Span[0] = message.HeaderByte;
                Memory<byte> truncatedMemory = memory[1..].WriteRemainingLength( remainingLength );
                message.Serialize( truncatedMemory.Span );
                await _channelStream.WriteAsync( memory, cancellationToken );
            }
        }

        public async Task<TReceive?> WaitMessageReceivedAsync<TReceive>( Func<TReceive, bool>? predicate, int timeoutMillisecond = -1 )
            where TReceive : class, IPacket
        {
            bool TypePredicate( IPacket packet )
                => packet is TReceive receive && (predicate?.Invoke( receive ) ?? true);
            return (TReceive?)await EventExtension.WaitAsync( _eSeqReceived, TypePredicate, (int)timeoutMillisecond );
        }
    }
}
