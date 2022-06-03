using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CK.MQTT.Client.ExtensionMethods
{
    public class RefCountingApplicationMessage
    {
        readonly IDisposable _disposable;
        int _counter = 1;
        public RefCountingApplicationMessage( ApplicationMessage applicationMessage, IDisposable disposable )
        {
            _disposable = disposable;
            ApplicationMessage = applicationMessage;
        }

        public ApplicationMessage ApplicationMessage { get; }

        public void IncrementRef()
        {
            Interlocked.Increment( ref _counter );
        }
        public void DecrementRef()
        {
            var refCount = Interlocked.Decrement( ref _counter );
            if( refCount == 0 )
            {
                Dispose();
            }
        }

        void Dispose()
        {
            ApplicationMessage.SetDisposed( typeof( RefCountingApplicationMessage ) );
            _disposable.Dispose();
        }
    }
}
