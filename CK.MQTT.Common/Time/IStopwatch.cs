using System;
using System.Diagnostics;

namespace CK.MQTT
{
    public interface IStopwatch
    {
        /// <inheritdoc cref="Stopwatch.Elapsed"/>
        public TimeSpan Elapsed { get; }
        /// <inheritdoc cref="Stopwatch.ElapsedMilliseconds"/>
        public long ElapsedMilliseconds { get; }
        /// <inheritdoc cref="Stopwatch.ElapsedTicks"/>
        public long ElapsedTicks { get; }
        /// <inheritdoc cref="Stopwatch.IsRunning"/>
        public bool IsRunning { get; }

        /// <inheritdoc cref="Stopwatch.Reset"/>
        public void Reset();

        /// <inheritdoc cref="Stopwatch.Restart"/>
        public void Restart();

        /// <inheritdoc cref="Stopwatch.Start"/>
        public void Start();

        /// <inheritdoc cref="Stopwatch.Stop"/>
        public void Stop();

    }
}
