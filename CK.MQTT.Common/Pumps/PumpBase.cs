using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Generalizes <see cref="InputPump"/> and <see cref="OutputPump"/>.
    /// </summary>
    public abstract class PumpBase
    {
        readonly PumppeteerBase _pumppeteer;
        Task? _readLoop;
        readonly CancellationTokenSource _stopSource = new CancellationTokenSource();

        private protected PumpBase( PumppeteerBase pumppeteer ) => _pumppeteer = pumppeteer;

        /// <summary>
        /// Gets the token that drives the run of this pump.
        /// </summary>
        protected CancellationToken StopToken => _stopSource.Token;

        /// <summary>
        /// Must be called at the end of the specialized constructors.
        /// </summary>
        /// <param name="loop">The running loop.</param>
        protected void SetRunningLoop( Task loop ) => _readLoop = loop;

        /// <summary>
        /// Triggers a disconnection from this pump.
        /// </summary>
        /// <param name="reason">The reason of the disconnection.</param>
        /// <returns>True if this call actually closed the connection, false if the connection has already been closed by a concurrent decision.</returns>
        public Task<bool> DisconnectAsync( DisconnectedReason reason )
        {
            _stopSource.Cancel();
            return _pumppeteer.PumpClose( reason );
        }

        /// <summary>
        /// This is called by the <see cref="Pumppeteer"/>.
        /// </summary>
        /// <returns>The awaitable.</returns>
        internal Task CloseAsync()
        {
            if( _stopSource.IsCancellationRequested ) return Task.CompletedTask;
            _stopSource.Cancel();
            return OnClosedAsync( _readLoop! );
        }

        /// <summary>
        /// Called when then this pump has been stopped: the <see cref="Loop"/>
        /// is being canceled.
        /// </summary>
        /// <param name="loop">
        /// The loop task. This method can wait for its termination if required.
        /// </param>
        /// <returns>The awaitable: by default this method simply returns the <paramref name="loop"/> task.</returns>
        protected virtual Task OnClosedAsync( Task loop ) => loop;
    }
}
