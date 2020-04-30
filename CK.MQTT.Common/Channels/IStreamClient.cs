using System;

namespace CK.MQTT.Common.Channels
{
    public interface IStreamClient : IDisposable
    {
        void Close();

        bool IsConnected { get; }
    }
}
