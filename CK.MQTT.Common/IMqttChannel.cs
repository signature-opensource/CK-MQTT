using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Channels
{
    public interface IMqttChannel : IDisposable
    {
        /// <summary>
        /// Disconnect the <see cref="IMqttChannel"/>.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the disconnection.
        /// The <see cref="IMqttChannel"/> should not be connected if canceled, it should only stop a "clean" disconnect <see cref="IMqttChannel"/>.</param>
        /// <returns></returns>
        void Close();

        /// <summary>
        /// Gets the stream.
        /// </summary>
        public Stream Stream { get; }
    }
}
