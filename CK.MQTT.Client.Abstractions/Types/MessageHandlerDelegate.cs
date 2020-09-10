using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// <see langword="delegate"/> called when the client receive an <see cref="IncomingMessage"/>.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that complete when the packet processing is finished.</returns>
    public delegate ValueTask MessageHandlerDelegate( string topic, PipeReader pipeReader, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken );
}
