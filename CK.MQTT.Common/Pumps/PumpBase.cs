using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Generalizes <see cref="InputPump"/> and <see cref="OutputPump"/>.
    /// </summary>
    public abstract class PumpBase : IDisposable
    {
        readonly CancellationTokenSource _stopSource = new();
        readonly CancellationTokenSource _closeSource = new();
        readonly Func<DisconnectReason, ValueTask> _onDisconnect;

        private protected PumpBase( Func<DisconnectReason, ValueTask> onDisconnect ) => _onDisconnect = onDisconnect;

        Task _workLoopTask = null!; //Always set in constructor

        /// <summary>
        /// Must be called at the end of the specialized constructors.
        /// </summary>
        /// <param name="loop">The running loop.</param>
        protected void SetRunningLoop( Task loop ) => _workLoopTask = loop;

        /// <summary>
        /// Gets the token that drives the run of this pump.
        /// When this Token is cancelled, the pump should complete it's work then close.
        /// </summary>
        public CancellationToken StopToken => _stopSource.Token;

        /// <summary>
        /// Order to stop initiated from the user.
        /// </summary>
        /// <returns></returns>
        public Task StopWorkAsync()
        {
            _stopSource.Cancel();
            return _workLoopTask;
        }

        /// <summary>
        /// Gets the token that close the pump.
        /// When this Token is cancelled, the pump should stop ASAP and the task complete.
        /// </summary>
        public CancellationToken CloseToken => _closeSource.Token;

        public virtual async Task CloseAsync()
        {
            if( !CancelTokens() ) return;
            await _workLoopTask;
        }

        internal protected async ValueTask SelfCloseAsync( DisconnectReason disconnectedReason )
        {
            if( !CancelTokens() ) return;
            await _onDisconnect( disconnectedReason );
        }

        /// <summary>
        /// Return true when the shutdown has been initiated.
        /// Return false when the shutdown is already in progress. It also mean this <see cref="PumpBase"/> is making the call to close.
        /// </summary>
        /// <returns></returns>
        internal bool CancelTokens()
        {
            if( _stopSource.IsCancellationRequested ) return false;
            _stopSource.Cancel();
            _closeSource.Cancel();
            return true;
        }

        public void Dispose()
        {
            _closeSource.Dispose();
            _stopSource.Dispose();
        }
    }
}
