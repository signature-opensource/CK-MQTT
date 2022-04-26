using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Pumps
{
    public class DuplexPump<TLeft, TRight> : IAsyncDisposable
        where TLeft : PumpBase
        where TRight : PumpBase
    {
        public TLeft Left { get; }
        public TRight Right { get; }

        readonly CancellationTokenSource _ctsClose;
        readonly CancellationTokenRegistration _ctsRegistration;
        public DuplexPump( TLeft left, TRight right )
        {
            Left = left;
            Right = right;
            _ctsClose = CancellationTokenSource.CreateLinkedTokenSource( Left.CloseToken, Right.CloseToken );
            _ctsRegistration = _ctsClose.Token.Register( Close );
        }

        void Close()
        {
            _ctsRegistration.Unregister();
            Left.CancelTokens();
            Right.CancelTokens();
        }

        /// <summary>
        /// Order to stop initiated form the user.
        /// </summary>
        /// <returns></returns>
        public Task StopWorkAsync() => Task.WhenAll( Left.StopWorkAsync(), Right.StopWorkAsync() );

        public Task CloseAsync() => Task.WhenAll( Left.CloseAsync(), Right.CloseAsync() );

        bool _isDispose;

        public async ValueTask DisposeAsync()
        {
            if( _isDispose ) return;
            Debug.Assert( Left.CloseToken.IsCancellationRequested );
            Debug.Assert( Right.CloseToken.IsCancellationRequested );
            _isDispose = true;
            var left = Left.DisposeAsync();
            var right = Right.DisposeAsync();
            await left;
            await right;
            _ctsClose.Dispose();
            _ctsRegistration.Dispose();
        }

        public bool IsRunning => !_isDispose && !Left.StopToken.IsCancellationRequested && !Right.StopToken.IsCancellationRequested;
        public bool IsClosed => _isDispose || Left.CloseToken.IsCancellationRequested || Right.CloseToken.IsCancellationRequested;
    }
}
