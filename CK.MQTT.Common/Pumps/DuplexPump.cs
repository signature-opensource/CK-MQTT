using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Pumps
{
    public class DuplexPump<T> : IAsyncDisposable where T : IState
    {
        readonly PumpBase _pumpA;
        readonly PumpBase _pumpB;

        readonly CancellationTokenSource _ctsClose;
        readonly CancellationTokenRegistration _ctsRegistration;
        public DuplexPump( T state, PumpBase pumpA, PumpBase pumpB )
        {
            State = state;
            _pumpA = pumpA;
            _pumpB = pumpB;
            _ctsClose = CancellationTokenSource.CreateLinkedTokenSource( _pumpA.CloseToken, _pumpB.CloseToken );
            _ctsRegistration = _ctsClose.Token.Register( Close );
        }

        void Close()
        {
            _ctsRegistration.Unregister();
            _pumpA.CancelTokens();
            _pumpB.CancelTokens();
        }

        /// <summary>
        /// Order to stop initiated form the user.
        /// </summary>
        /// <returns></returns>
        public Task StopWorkAsync() => Task.WhenAll( _pumpA.StopWorkAsync(), _pumpB.StopWorkAsync() );

        bool _isDispose;



        /// <summary>
        /// Called by the client itself.
        /// </summary>
        /// <returns></returns>
        public async ValueTask DisposeAsync()
        {
            await Task.WhenAll( _pumpA.CloseAsync(), _pumpB.CloseAsync() );
            await State.CloseAsync();
            Dispose();
        }

        public void Dispose()
        {
            _isDispose = true;
            _pumpA.Dispose();
            _pumpB.Dispose();
            _ctsClose.Dispose();
            _ctsRegistration.Dispose();
        }


        public bool IsRunning => !_isDispose && !_pumpA.StopToken.IsCancellationRequested && !_pumpB.StopToken.IsCancellationRequested;
        public bool IsClosed => _isDispose || _pumpA.CloseToken.IsCancellationRequested || _pumpB.CloseToken.IsCancellationRequested;

        public T State { get; }
    }
}
