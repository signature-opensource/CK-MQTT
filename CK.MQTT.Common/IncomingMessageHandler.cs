using CK.Core;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using CK.MQTT.Abstractions.Serialisation;

namespace CK.MQTT.Common.Channels
{
    /// </summary>
    /// <param name="m">The monitor to log activity in a reflex.</param>
    /// <param name="header">The byte header of the incoming packet.</param>
    /// <param name="packetSize">The packet size of the incoming packet.</param>
    /// <param name="reader">The PipeReader of the incoming transmission.</param>
    /// <param name="currentBuffer">The buffer</param>
    /// <returns></returns>
    public delegate ValueTask Reflex( IActivityMonitor m, IncomingMessageHandler sender, byte header, int packetSize, PipeReader reader );

    public class IncomingMessageHandler
    {
        readonly PipeReader _pipeReader;
        readonly Task _readLoop;

        public IncomingMessageHandler( PipeReader pipeReader, Reflex reflex )
        {
            _pipeReader = pipeReader;
            CurrentReflex = reflex;
            _readLoop = ReadLoop();
        }

        /// <summary>
        /// Current <see cref="Reflex"/> that will be run on the incoming messages.
        /// </summary>
        public Reflex CurrentReflex { get; set; }


        readonly SequentialEventHandlerSender<IncomingMessageHandler, DisconnectedReason> _eSeqDisconnect
            = new SequentialEventHandlerSender<IncomingMessageHandler, DisconnectedReason>();
        public event SequentialEventHandler<IncomingMessageHandler, DisconnectedReason> Disconnected
        {
            add => _eSeqDisconnect.Add( value );
            remove => _eSeqDisconnect.Remove( value );
        }

        OperationStatus TryParsePacketHeader( ReadOnlySequence<byte> sequence, out byte header, out int length, out SequencePosition position )
        {
            SequenceReader<byte> reader = new SequenceReader<byte>( sequence );
            length = 0;
            if( !reader.TryRead( out header ) )
            {
                position = reader.Position;
                return OperationStatus.NeedMoreData;
            }
            return reader.TryReadMQTTRemainingLength( out length, out position );
        }

        void OnProtocolError( IActivityMonitor m )
        {
            _eSeqDisconnect.Raise( m, this, DisconnectedReason.ProtocolError );
        }

        async Task ReadLoop()
        {
            ActivityMonitor m = new ActivityMonitor();
            try
            {
                while( true )
                {
                    ReadResult read = await _pipeReader.ReadAsync();
                    if( read.IsCanceled ) return;
                    OperationStatus res = TryParsePacketHeader( read.Buffer, out byte header, out int length, out SequencePosition position ); //this guy require 2-5 bytes
                    using( m.OpenTrace( $"Incoming packet of {length} bytes." ) )
                    {
                        if( res == OperationStatus.InvalidData )
                        {
                            m.Error( "Corrupted Stream." );
                            return;
                        }
                        if( res == OperationStatus.NeedMoreData )
                        {
                            if( read.IsCompleted )
                            {
                                m.Error( "Unexpected End Of Stream." );
                                return;
                            }
                            _pipeReader.AdvanceTo( read.Buffer.Start, read.Buffer.End );//Mark data observed, so we will wait new data.
                            continue;
                        }
                        else
                        {
                            _pipeReader.AdvanceTo( position );
                            await CurrentReflex( m, this, header, length, _pipeReader );
                        }
                    }
                }
            }
            catch( Exception e )
            {
                m.Error( e );
                _pipeReader.Complete( e );
                OnProtocolError( m );
            }
        }

        public Task Stop( CancellationToken cancellationToken )
        {
            cancellationToken.Register( () => _pipeReader.CancelPendingRead() );
            if( cancellationToken.IsCancellationRequested ) _pipeReader.CancelPendingRead();
            _pipeReader.Complete();
            return _readLoop;
        }
    }
}
