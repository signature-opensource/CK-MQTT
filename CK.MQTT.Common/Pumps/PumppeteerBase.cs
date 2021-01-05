using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// This class manages the lifecycle of a <see cref="InputPump"/>/<see cref="OutputPump"/> pair.
    /// </summary>
    public abstract class PumppeteerBase
    {
        // Must never be disposed!
        static readonly CancellationTokenSource _signaled = new CancellationTokenSource( 0 );
        readonly object _closeLock = new object();

        CancellationTokenSource _closed = _signaled;

        /// <summary>
        /// Initializes a specialized <see cref="PumppeteerBase"/>.
        /// </summary>
        /// <param name="configuration">The MQTT basic configuration.</param>
        protected PumppeteerBase( MqttConfigurationBase configuration ) => Configuration = configuration;

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        internal protected MqttConfigurationBase Configuration { get; }

        /// <summary>
        /// Exposes a token that is cancelled when this "pumppeteer" is closed.
        /// </summary>
        protected CancellationToken CloseToken => _closed.Token;

        protected async Task<bool> CloseAsync( DisconnectedReason reason )
        {
            lock( _closeLock )
            {
                if( _closed.IsCancellationRequested ) return false;
                _closed.Cancel();
            }
            await OnClosingAsync( reason );
            await OnClosed( reason );
            return true;
        }

        protected void Open()
        {
            if( !_closed.IsCancellationRequested ) throw new InvalidOperationException( "The pumppeteer was already opened." );
            _closed = new CancellationTokenSource();
        }

        /// <summary>
        /// Gets whether this "pumppeteer" is currently connected.
        /// </summary>
        public bool IsConnected => !_closed.IsCancellationRequested;

        /// <summary>
        /// Called by the input or output pump whenever they need to close the connection to the remote part
        /// for any reason.
        /// </summary>
        /// <param name="reason">The reason of the disconnection.</param>
        /// <returns>True if this call actually closed the connection, false if the connection has already been closed by a concurrent decision.</returns>
        internal Task<bool> PumpClose( DisconnectedReason reason ) => CloseAsync( reason );

        /// <summary>
        /// Closed when this "pumppeteer" has been closed.
        /// This method calls the <see cref="DisconnectedHandler"/> (if any and if the reason is not <see cref="DisconnectedReason.None"/>).
        /// </summary>
        /// <param name="reason">The disconnection reason.</param>
        protected virtual ValueTask OnClosed( DisconnectedReason reason )
        {
            if( reason != DisconnectedReason.None ) DisconnectedHandler?.Invoke( reason );
            return default;
        }

        /// <summary>
        /// Called whenever, for any reason, this is closed.
        /// </summary>
        public Disconnected? DisconnectedHandler { get; set; }

        /// <summary>
        /// Called before the pumps are closed.
        /// </summary>
        /// <param name="reason">The disconnection reason.</param>
        /// <returns>The awaitable.</returns>
        protected virtual ValueTask OnClosingAsync( DisconnectedReason reason ) => default;
    }
}
