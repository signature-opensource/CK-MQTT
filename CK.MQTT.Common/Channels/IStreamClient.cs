using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Common.Channels
{
    public interface IStreamClient : IDisposable
    {
        void Close();

        bool IsConnected { get; }
    }
}
