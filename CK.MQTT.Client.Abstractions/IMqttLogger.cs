using System;

namespace CK.MQTT
{
    public interface IMqttLogger
    {
        IDisposable OpenInfo( string message );
        IDisposable OpenTrace( string message );
        void Trace( string message );
        void Info( string message );
        void Warn( string message );
        void Error( string message );
        void Error( Exception? e );
        void Error( string message, Exception? e );
    }
}
