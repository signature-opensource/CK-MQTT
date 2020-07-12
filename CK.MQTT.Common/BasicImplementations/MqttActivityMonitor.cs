using CK.Core;
using System;

namespace CK.MQTT
{
    /// <summary>
    /// Simple wrapper of an <see cref="IActivityMonitor"/> to a <see cref="IMqttLogger"/>.
    /// </summary>
    public class MqttActivityMonitor : IMqttLogger
    {
        readonly IActivityMonitor _m;

        /// <summary>
        /// Instantiate this wrapper.
        /// </summary>
        /// <param name="m">The <see cref="IActivityMonitor"/> to wrap.</param>
        public MqttActivityMonitor( IActivityMonitor m )
        {
            _m = m;
        }

        /// <inheritdoc/>
        public void Error( string message ) => _m.Error( message );

        /// <inheritdoc/>
        public void Error( Exception? e ) => _m.Error( e );

        /// <inheritdoc/>
        public void Error( string? message, Exception? e ) => _m.Error( message, e );

        /// <inheritdoc/>
        public void Info( string message ) => _m.Info( message );

        /// <inheritdoc/>
        public IDisposable OpenInfo( string message ) => _m.OpenInfo( message );

        /// <inheritdoc/>
        public IDisposable OpenTrace( string message ) => _m.OpenTrace( message );

        /// <inheritdoc/>
        public void Trace( string message ) => _m.Trace( message );

        /// <inheritdoc/>
        public void Warn( string message ) => _m.Warn( message );
    }
}
