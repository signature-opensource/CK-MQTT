using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Generalizes <see cref="InputPump"/> and <see cref="OutputPump"/>.
    /// </summary>
    public abstract class PumpBase : IDisposable
    {
        Task _readLoop = null!;
        readonly CancellationTokenSource _stopSource = new();
        readonly CancellationTokenSource _closeSource = new();
        readonly Func<DisconnectReason, ValueTask> _onDisconnect;

        private protected PumpBase( Func<DisconnectReason, ValueTask> onDisconnect ) => _onDisconnect = onDisconnect;

        /// <summary>
        /// Must be called at the end of the specialized constructors.
        /// </summary>
        /// <param name="loop">The running loop.</param>
        protected void SetRunningLoop( Task loop ) => _readLoop = loop;

        /// <summary>
        /// Gets the token that drives the run of this pump.
        /// When this Token is cancelled, the pump should complete it's work then close.
        /// </summary>
        public CancellationToken StopToken => _stopSource.Token;

        public Task StopWorkAsync()
        {
            _stopSource.Cancel();
            return _readLoop;
        }

        /// <summary>
        /// Gets the token that close the pump.
        /// When this Token is cancelled, the pump should stop ASAP and the task complete.
        /// </summary>
        public CancellationToken CloseToken => _closeSource.Token;

        public virtual Task CloseAsync()
        {
            CancelTokens();
            return _readLoop;
        }

        internal protected ValueTask SelfCloseAsync( DisconnectReason disconnectedReason )
        {
            CancelTokens();
            return _onDisconnect( disconnectedReason );
        }

        internal void CancelTokens()
        {
            _stopSource.Cancel();
            _closeSource.Cancel();
        }

        public void Dispose()
        {
            _closeSource.Dispose();
            _stopSource.Dispose();
        }
    }
}
