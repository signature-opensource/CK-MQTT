using CK.Core;
using CK.MQTT.Client.Tests.Helpers;
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests
{
    public class BytePerByteLoopback : LoopBack
    {
        public BytePerByteLoopback()
        {
            Pipe input = new();
            Pipe intermediary = new();
            Pipe output = new();
            _ = WorkLoop( input, intermediary );
            DuplexPipe = new DuplexPipe( intermediary.Reader, output.Writer );
            TestDuplexPipe = new DuplexPipe( output.Reader, input.Writer );
        }

        readonly byte[] _buffer = new byte[1];

        public override IDuplexPipe TestDuplexPipe { get; set; }
        public override IDuplexPipe DuplexPipe { get; set; }

        async Task WorkLoop( Pipe input, Pipe bytePerByte )
        {

            while( true )
            {
                ReadResult result = await input.Reader.ReadAsync();
                foreach( ReadOnlyMemory<byte> memory in result.Buffer )
                {
                    for( int i = 0; i < memory.Length; i++ )
                    {
                        _buffer[0] = memory.Span[i];
                        await bytePerByte.Writer.WriteAsync( _buffer );
                        await bytePerByte.Writer.FlushAsync();
                    }
                }
                input.Reader.AdvanceTo( result.Buffer.End );
                if( result.IsCompleted ) return;
                if( result.IsCanceled ) return;
            }
        }
    }
}
