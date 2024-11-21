using System;

namespace CK.MQTT.Client;

public readonly struct VolatileApplicationMessage : IDisposable
{
    readonly IDisposable _disposable;

    public VolatileApplicationMessage( ApplicationMessage applicationMessage, IDisposable disposable )
    {
        _disposable = disposable;
        Message = applicationMessage;
    }

    public ApplicationMessage Message { get; }

    public void Dispose()
    {
        Message.SetDisposed( typeof( VolatileApplicationMessage ) );
        _disposable.Dispose();
    }
}
