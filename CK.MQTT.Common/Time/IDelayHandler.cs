using System;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable RS0030 // Do not used banned APIs
namespace CK.MQTT
{
    public interface IDelayHandler
    {
        /// <inheritdoc cref="Task.Delay(int)"/>
        public Task Delay( int millisecondsDelay );

        /// <inheritdoc cref="Task.Delay(int,CancellationToken)"/>
        public Task Delay( int millisecondsDelay, CancellationToken cancellationToken );

        /// <inheritdoc cref="Task.Delay(TimeSpan)"/>
        public Task Delay( TimeSpan delay );

        /// <inheritdoc cref="Task.Delay(TimeSpan,CancellationToken)"/>
        public Task Delay( TimeSpan delay, CancellationToken cancellationToken );

    }
}
#pragma warning restore RS0030 // Do not used banned APIs
