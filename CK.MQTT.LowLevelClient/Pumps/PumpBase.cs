using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Generalizes <see cref="InputPump"/> and <see cref="OutputPump"/>.
    /// </summary>
    public abstract class PumpBase
    {
        protected readonly MessageExchanger MessageExchanger;

        private protected PumpBase( MessageExchanger messageExchanger )
        {
            MessageExchanger = messageExchanger;
        }

        /// <summary>
        /// Must be called at the end of the specialized constructors.
        /// </summary>
        /// <param name="loop">The running loop.</param>
        [MemberNotNull(nameof(WorkTask))]
        protected void SetRunningLoop( Task loop ) => WorkTask = loop;

        public Task? WorkTask { get; private set; }

        internal protected async ValueTask SelfCloseAsync( DisconnectReason disconnectedReason )
        {
            MessageExchanger.StopTokenSource.Cancel();
            await MessageExchanger.FinishSelfDisconnectAsync( disconnectedReason );
        }

       
    }
}
