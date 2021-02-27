// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;

namespace System.IO.Pipelines
{
    public class DuplexPipe : IDuplexPipe
    {
        public DuplexPipe( Stream stream, StreamPipeReaderOptions? readerOptions = null, StreamPipeWriterOptions? writerOptions = null )
        {
            Input = PipeReader.Create( stream, readerOptions );
            Output = PipeWriter.Create( stream, writerOptions );
        }

        public DuplexPipe( PipeReader input, PipeWriter output )
        {
            Input = input;
            Output = output;
        }
        
        public PipeReader Input { get; }

        public PipeWriter Output { get; }

        public void Dispose()
        {
            Input.Complete();
            Output.Complete();
        }
    }
}
