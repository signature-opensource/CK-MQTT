using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public delegate ValueTask Reflex( IMqttLogger m, IncomingMessageHandler sender, byte header, int packetSize, PipeReader reader );

    public class IncomingMessageHandler : IDisposable
    {
        readonly Action<IMqttLogger, DisconnectedReason> _stopClient;
        readonly PipeReader _pipeReader;
        readonly Task _readLoop;
        readonly CancellationTokenSource _cleanStop = new CancellationTokenSource();
        bool _closed;
        public IncomingMessageHandler( IMqttLogger inputLogger, Action<IMqttLogger, DisconnectedReason> stopClient, PipeReader pipeReader, Reflex reflex )
        {
            _stopClient = stopClient;
            _pipeReader = pipeReader;
            CurrentReflex = reflex;
            _readLoop = ReadLoop( inputLogger );
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

        async Task ReadLoop( IMqttLogger m )
        {
            using( m.OpenInfo( "Listening Incoming Messages..." ) )
            {
                while( !_cleanStop.IsCancellationRequested )
                {
                    ReadResult read = await _pipeReader.ReadAsync( _cleanStop.Token );
                    if( read.IsCanceled )
                    {
                        m.Trace( "Read Cancelled, exiting." );
                        return;
                    }
                    OperationStatus res = TryParsePacketHeader( read.Buffer, out byte header, out int length, out SequencePosition position ); //this guy require 2-5 bytes
                    if( res == OperationStatus.InvalidData )
                    {
                        CloseWithError( m, DisconnectedReason.ProtocolError, "Corrupted Stream." );
                        return;
                    }
                    if( res == OperationStatus.NeedMoreData )
                    {
                        if( read.IsCompleted )
                        {
                            if( read.Buffer.Length == 0 )
                            {
                                m.Info( "Remote closed channel." );
                                CloseInternal( m, DisconnectedReason.RemoteDisconnected, false );
                            }
                            else
                            {
                                CloseWithError( m, DisconnectedReason.RemoteDisconnected, "Unexpected End Of Stream." );
                            }

                            return;
                        }
                        _pipeReader.AdvanceTo( read.Buffer.Start, read.Buffer.End );//Mark data observed, so we will wait new data.
                        continue;
                    }
                    _pipeReader.AdvanceTo( position );
                    using( m.OpenTrace( $"Incoming packet of {length} bytes." ) )
                    {
                        try
                        {
                            await CurrentReflex( m, this, header, length, _pipeReader );
                        }
                        catch( Exception e )
                        {
                            CloseWithError( m, DisconnectedReason.UnspecifiedError, exception: e );
                            return;
                        }
                    }
                }
            }
        }

        void CloseWithError( IMqttLogger m, DisconnectedReason reason, string? error = null, Exception? exception = null )
        {
            if( _closed ) return;
            m.Error( error ?? string.Empty, exception );
            CloseInternal( m, reason, false );
        }

        void CloseInternal( IMqttLogger m, DisconnectedReason reason, bool waitLoop )
        {
            lock( _readLoop )
            {
                if( _closed ) return;
                _closed = true;
            }
            m.Trace( $"Closing {nameof( IncomingMessageHandler )}." );
            _cleanStop.Cancel();
            _pipeReader.Complete();
            _pipeReader.CancelPendingRead();
            if( waitLoop && !_readLoop.IsCompleted )
            {
                m.Warn( $"{nameof( IncomingMessageHandler )} main loop is taking time to exit..." );
                _readLoop.Wait();
            }
            _stopClient( m, reason );
        }

        public void Dispose() => _pipeReader.Complete();
        public void Close( IMqttLogger m, DisconnectedReason reason ) => CloseInternal( m, reason, true );
    }
}
