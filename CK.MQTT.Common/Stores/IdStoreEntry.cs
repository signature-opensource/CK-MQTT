using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Common.Stores
{
    public struct IdStoreEntry<T>
    {
        public int NextId;
        public int PreviousId;
        public T Content;
    }
}
