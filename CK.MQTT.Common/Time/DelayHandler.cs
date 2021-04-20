using System;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable RS0030 // Do not used banned APIs

namespace CK.MQTT
{
    class DelayHandler : IDelayHandler
    {
        DelayHandler() { }
        public Task Delay( int millisecondsDelay ) => Task.Delay( millisecondsDelay );

        public Task Delay( int millisecondsDelay, CancellationToken cancellationToken ) => Task.Delay( millisecondsDelay, cancellationToken );

        public Task Delay( TimeSpan delay ) => Task.Delay( delay );

        public Task Delay( TimeSpan delay, CancellationToken cancellationToken ) => Task.Delay( delay, cancellationToken );

        internal static IDelayHandler Default { get; } = new DelayHandler();
    }
}
#pragma warning restore RS0030 // Do not used banned APIs
