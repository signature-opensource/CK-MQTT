using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Abstractions.Types
{
    public enum StopMode
    {
        Dirty = 0,
        Clean = 1,
        CompleteJobs = 3
    }
}
