using CK.Core;
using System;
using System.IO;
using System.Threading;

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
        void Close( IActivityMonitor m  );

        /// <summary>
        /// 
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public bool IsConnected( IActivityMonitor m );

        /// <summary>
        /// Gets the stream.
        /// </summary>
        public Stream Stream { get; }
    }
}
