using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Generalizes <see cref="InputPump"/> and <see cref="OutputPump"/>.
    /// </summary>
    public abstract class PumpBase
    {
        readonly Func<DisconnectReason, ValueTask> _closeHandler;

        private protected PumpBase( Func<DisconnectReason, ValueTask> closeHandler)
        {
            _closeHandler = closeHandler;
        }

        /// <summary>
        /// Must be called at the end of the specialized constructors.
        /// </summary>
        /// <param name="loop">The running loop.</param>
        [MemberNotNull( nameof( WorkTask ) )]
        public void StartPumping(CancellationToken stopToken, CancellationToken closeToken)
        {
            WorkTask = WorkLoopAsync( stopToken, closeToken );
        }

        protected abstract Task WorkLoopAsync( CancellationToken stopToken, CancellationToken closeToken );

        public Task? WorkTask { get; private set; }

        /// <summary>
        /// Called by the pump to trigger the client disconnection.
        /// </summary>
        /// <param name="disconnectedReason"></param>
        /// <returns></returns>
        protected async ValueTask SelfDisconnectAsync( DisconnectReason disconnectedReason )
            => await _closeHandler( disconnectedReason );


    }
}
