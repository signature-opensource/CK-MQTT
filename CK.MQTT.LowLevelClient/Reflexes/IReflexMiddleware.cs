using CK.MQTT.Client;
using CK.MQTT.Pumps;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IReflexMiddleware
    {
        /// <param name="sender">The <see cref="InputPump"/> that called this middleware.</param>
        /// <param name="header">The first byte of the packet.</param>
        /// <param name="packetLength">The length of the incoming packet.</param>
        /// <param name="pipeReader">The pipe reader to use to read the packet data.</param>
        /// <param name="next">The next middleware.</param>
        /// <param name="sink"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <remarks>
        /// If a middleware advance the <see cref="PipeReader"/>, the next middleware can't be aware of it.
        /// </remarks>
        ValueTask<(OperationStatus, bool)> ProcessIncomingPacketAsync( IMQTT3Sink sink, InputPump sender, byte header, uint packetLength, PipeReader pipeReader, CancellationToken cancellationToken );
    }
}
