using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Generalizes <see cref="InputPump"/> and <see cref="OutputPump"/>.
    /// </summary>
    public abstract class PumpBase : IAsyncDisposable
    {
        readonly CancellationTokenSource _stopSource = new();
        readonly CancellationTokenSource _closeSource = new();
        protected readonly MessageExchanger MessageExchanger;

        private protected PumpBase( MessageExchanger messageExchanger )
        {
            MessageExchanger = messageExchanger;
        }

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
        public async Task StopWorkAsync()
        {
            if( _stopSource.IsCancellationRequested ) return;
            _stopSource.Cancel();
            await _workLoopTask;
            _closeSource.Cancel();
        }

        /// <summary>
        /// Gets the token that close the pump.
        /// When this Token is cancelled, the pump should stop ASAP and the task complete.
        /// </summary>
        public CancellationToken CloseToken => _closeSource.Token;

        public virtual async Task CloseAsync()
        {
            CancelTokens();
            await _workLoopTask;
        }

        internal protected async ValueTask SelfCloseAsync( DisconnectReason disconnectedReason )
        {
            CancelTokens();
            await MessageExchanger.SelfDisconnectAsync( disconnectedReason );
        }

        /// <summary>
        /// Return true when the shutdown has been initiated.
        /// Return false when the shutdown is already in progress. It also mean this <see cref="PumpBase"/> is making the call to close.
        /// </summary>
        /// <returns></returns>
        internal void CancelTokens()
        {
            _stopSource!.Cancel();
            _closeSource.Cancel();
        }

        public virtual ValueTask DisposeAsync()
        {
            Debug.Assert( _workLoopTask.IsCompleted );
            _closeSource.Dispose();
            _stopSource.Dispose();
            return new ValueTask();
        }
    }
}
