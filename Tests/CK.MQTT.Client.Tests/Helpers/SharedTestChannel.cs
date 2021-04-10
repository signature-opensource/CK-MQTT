using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    class SharedTestChannel : IMqttChannel
    {
        public bool IsConnected => true;

        public IDuplexPipe DuplexPipe => throw new NotImplementedException();

        public void Close( IInputLogger? m )
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
