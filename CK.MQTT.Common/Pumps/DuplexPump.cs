using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Pumps
{
    public class DuplexPump<T> : IDisposable where T : IState
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
            _ = _ctsRegistration.Unregister();
            _pumpA.Close();
            _pumpB.Close();
        }

        public Task StopWork() => Task.WhenAll( _pumpA.StopWork(), _pumpB.StopWork() );

        public async Task CloseAsync()
        {
            await Task.WhenAll( _pumpA.CloseAsync(), _pumpB.CloseAsync() );
            await State.CloseAsync();
        }

        public void Dispose()
        {
            _pumpA.Dispose();
            _pumpB.Dispose();
            _ctsClose.Dispose();
            _ctsRegistration.Dispose();
        }

        public bool IsRunning => !_pumpA.StopToken.IsCancellationRequested && !_pumpB.StopToken.IsCancellationRequested;
        public bool IsClosed => _pumpA.CloseToken.IsCancellationRequested  || _pumpB.CloseToken.IsCancellationRequested;

        public T State { get; }
    }

    public interface IState
    {
        public Task CloseAsync();
    }
}
