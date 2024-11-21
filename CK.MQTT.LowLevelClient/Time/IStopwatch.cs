using System;

namespace CK.MQTT;

public interface IStopwatch
{
    /// <inheritdoc cref="System.Diagnostics.Stopwatch.Elapsed"/>
    public TimeSpan Elapsed { get; }
    /// <inheritdoc cref="System.Diagnostics.Stopwatch.ElapsedMilliseconds"/>
    public long ElapsedMilliseconds { get; }
    /// <inheritdoc cref="System.Diagnostics.Stopwatch.ElapsedTicks"/>
    public long ElapsedTicks { get; }
    /// <inheritdoc cref="System.Diagnostics.Stopwatch.IsRunning"/>
    public bool IsRunning { get; }

    /// <inheritdoc cref="System.Diagnostics.Stopwatch.Reset"/>
    public void Reset();

    /// <inheritdoc cref="System.Diagnostics.Stopwatch.Restart"/>
    public void Restart();

    /// <inheritdoc cref="System.Diagnostics.Stopwatch.Start"/>
    public void Start();

    /// <inheritdoc cref="System.Diagnostics.Stopwatch.Stop"/>
    public void Stop();

}
