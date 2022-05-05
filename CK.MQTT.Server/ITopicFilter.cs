using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public interface ITopicFilter
    {
        public bool IsFiltered( string topic );
    }
}
