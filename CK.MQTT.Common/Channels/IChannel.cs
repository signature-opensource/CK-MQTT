using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Channels
{
    public interface IChannel : IDisposable
    {
        /// <summary>
        /// Connect the <see cref="IChannel"/>.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the connection.</param>
        /// <returns>A <see cref="ValueTask"/> completing when the <see cref="IChannel"/> is connected.</returns>
        ValueTask ConnectAsync( CancellationToken cancellationToken );

        /// <summary>
        /// Disconnect the <see cref="IChannel"/>.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the disconnection.
        /// The <see cref="IChannel"/> should not be connected if canceled, it should only stop a "clean" disconnect <see cref="IChannel"/>.</param>
        /// <returns></returns>
        ValueTask DisconnectAsync( CancellationToken cancellationToken );

        [All]
        bool IsConnected { get; }

        /// <summary>
        /// Gets the stream.
        /// May be <see cref="null"/> when disconnected.
        /// </summary>
        public Stream? Stream { get; }
        //Why Nullable ?:
        //      A not-yet connected Stream is null, connection was not made, the NetworkStream has not been instantiated yet.
        //      If DisconnectAsync is called, a NullRef would be throwed instead of a DisconnectedException, by being signaled in the documentation.
        //      It doesn't replace IsConnected
    }
}
