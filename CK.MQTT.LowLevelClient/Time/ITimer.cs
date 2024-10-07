using System;

namespace CK.MQTT;

public interface ITimer : IAsyncDisposable, IDisposable
{
    public bool Change( int dueTime, int period );

    public bool Change( long dueTime, long period );
    public bool Change( TimeSpan dueTime, TimeSpan period );
    public bool Change( uint dueTime, uint period );
}
