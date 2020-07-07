using CK.Core;
using System;

namespace CK.MQTT
{
    public class MqttActivityMonitor : IMqttLogger
    {
        readonly IActivityMonitor _m;

        public MqttActivityMonitor( IActivityMonitor m )
        {
            _m = m;
        }

        public void Error( string message ) => _m.Error( message );

        public void Error( Exception? e ) => _m.Error( e );

        public void Error( string message, Exception? e ) => _m.Error( message, e );

        public void Info( string message ) => _m.Info( message );

        public IDisposable OpenInfo( string message ) => _m.OpenInfo( message );
        public IDisposable OpenTrace( string message ) => _m.OpenTrace( message );

        public void Trace( string message ) => _m.Trace( message );

        public void Warn( string message ) => _m.Warn( message );
    }
}
