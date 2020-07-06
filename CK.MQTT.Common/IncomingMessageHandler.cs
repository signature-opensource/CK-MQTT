using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// </summary>
    /// <param name="m">The monitor to log activity in a reflex.</param>
    /// <param name="header">The byte header of the incoming packet.</param>
    /// <param name="packetSize">The packet size of the incoming packet.</param>
    /// <param name="reader">The PipeReader of the incoming transmission.</param>
    /// <param name="currentBuffer">The buffer</param>
    /// <returns></returns>
    public delegate ValueTask Reflex( IMqttLogger m, IncomingMessageHandler sender, byte header, int packetSize, PipeReader reader );

    public class IncomingMessageHandler
    {
        readonly Action<IMqttLogger, DisconnectedReason> _stopClient;
        readonly PipeReader _pipeReader;
        readonly Task _readLoop;
        readonly CancellationTokenSource _cleanStop = new CancellationTokenSource();
        bool _closed;
        public IncomingMessageHandler( IMqttLoggerFactory loggerFactory, Action<IMqttLogger, DisconnectedReason> stopClient, PipeReader pipeReader, Reflex reflex )
        {
            _stopClient = stopClient;
            _pipeReader = pipeReader;
            CurrentReflex = reflex;
            _readLoop = ReadLoop( loggerFactory );
        }

        /// <summary>
        /// Current <see cref="Reflex"/> that will be run on the incoming messages.
        /// </summary>
        public Reflex CurrentReflex { get; set; }

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

        async Task ReadLoop( IMqttLoggerFactory loggerFactory )
        {
            IMqttLogger l = loggerFactory.Create();
            while( !_cleanStop.IsCancellationRequested )
            {
                ReadResult read = await _pipeReader.ReadAsync( _cleanStop.Token );
                if( read.IsCanceled ) return;
                OperationStatus res = TryParsePacketHeader( read.Buffer, out byte header, out int length, out SequencePosition position ); //this guy require 2-5 bytes
                if( res == OperationStatus.InvalidData )
                {
                    CloseWithError( l, DisconnectedReason.ProtocolError, "Corrupted Stream." );
                    return;
                }
                if( res == OperationStatus.NeedMoreData )
                {
                    if( read.IsCompleted )
                    {
                        if( read.Buffer.Length == 0 )
                        {
                            l.Info( "Remote closed channel." );
                            Close( l, DisconnectedReason.RemoteDisconnected );
                        }
                        else CloseWithError( l, DisconnectedReason.RemoteDisconnected, "Unexpected End Of Stream." );
                        return;
                    }
                    _pipeReader.AdvanceTo( read.Buffer.Start, read.Buffer.End );//Mark data observed, so we will wait new data.
                    continue;
                }
                _pipeReader.AdvanceTo( position );
                using( l.OpenTrace( $"Incoming packet of {length} bytes." ) )
                {
                    try
                    {
                        await CurrentReflex( l, this, header, length, _pipeReader );
                    }
                    catch( Exception e )
                    {
                        CloseWithError( l, DisconnectedReason.UnspecifiedError, exception: e );
                        return;
                    }
                }
            }
        }

        void CloseWithError( IMqttLogger m, DisconnectedReason reason, string? error = null, Exception? exception = null )
        {
            if( _closed ) return;
            m.Error( error ?? string.Empty, exception );
            Close( m, reason );
        }

        public void Close( IMqttLogger m, DisconnectedReason reason )
        {
            if( _closed ) return;
            _closed = true;
            _cleanStop.Cancel();
            _pipeReader.Complete();
            _stopClient( m, reason );
        }
    }
}
