using System;

namespace CK.MQTT
{
    /// <summary>
    /// Interface for the mqtt logger.
    /// </summary>
    public interface IMqttLogger
    {
        /// <summary>
        /// Open a log group with a log level of info.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <returns>A <see cref="IDisposable"/>, disposing it should close the log group.</returns>
        IDisposable? OpenInfo( string message );

        /// <summary>
        /// Open a log group with a log level of Trace.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <returns>A <see cref="IDisposable"/>, disposing it should close the log group.</returns>
        IDisposable? OpenTrace( string message );

        /// <summary>
        /// Open a log group with a log level of Error.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <returns>A <see cref="IDisposable"/>, disposing it should close the log group.</returns>
        IDisposable? OpenError( string message );

        /// <summary>
        /// Log a message with the trace log level.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Trace( string message );

        /// <summary>
        /// Log a message with the info log level.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Info( string message );

        /// <summary>
        /// Log a message with the warn log level.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Warn( string message );

        /// <summary>
        /// Log a message with the error log level.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Error( string message );

        /// <summary>
        /// Log an exception with the error log level.
        /// </summary>
        /// <param name="e">The exception to log.</param>
        void Error( Exception? e );

        /// <summary>
        /// Log a message or/and an exception with the error log level.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="e">The error to log.</param>
        void Error( string? message = null, Exception? e = null );
    }
}
