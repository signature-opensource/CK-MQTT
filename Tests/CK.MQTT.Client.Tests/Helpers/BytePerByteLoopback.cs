using CK.MQTT.Client.Tests.Helpers;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests
{
    [ExcludeFromCodeCoverage]
    public class BytePerByteLoopback : LoopBack
    {
        public BytePerByteLoopback()
        {
            const int size = 16;
            Pipe input = new( new PipeOptions( minimumSegmentSize: size ) );
            Pipe intermediary = new( new PipeOptions( minimumSegmentSize: size ) );
            Pipe output = new( new PipeOptions( minimumSegmentSize: size ) );

            _ = WorkLoop( input.Writer, input.Reader, intermediary.Writer );
            DuplexPipe = new DuplexPipe( intermediary.Reader, output.Writer );
            TestDuplexPipe = new DuplexPipe( output.Reader, input.Writer );

        }

        readonly byte[] _buffer = new byte[1];

        public override IDuplexPipe TestDuplexPipe { get; set; }
        public override IDuplexPipe DuplexPipe { get; set; }

        async Task WorkLoop( PipeWriter inputWriter, PipeReader inputReader, PipeWriter bytePerByte )
        {
            while( true )
            {
                ReadResult result = await inputReader.ReadAsync();
                foreach( ReadOnlyMemory<byte> memory in result.Buffer )
                {
                    for( int i = 0; i < memory.Length; i++ )
                    {
                        _buffer[0] = memory.Span[i];
                        await bytePerByte.WriteAsync( _buffer );
                        await bytePerByte.FlushAsync();
                    }
                }
                inputReader.AdvanceTo( result.Buffer.End );
                if( result.IsCompleted ) break;
                if( result.IsCanceled ) break;
            }
            bytePerByte.Complete();
            inputWriter.Complete();
        }
    }
}
