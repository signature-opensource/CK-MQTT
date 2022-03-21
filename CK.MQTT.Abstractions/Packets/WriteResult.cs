using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Packets
{
    /// <summary>
    /// Result of a Write Operation From the Store t.
    /// </summary>
    public enum WriteResult
    {
        /// <summary>
        /// The <see cref="IOutgoingPacket"/> is expired. No write operation has been made.
        /// </summary>
        Expired,
        /// <summary>
        /// The <see cref="IOutgoingPacket"/> has been written. It may or may not be reused.
        /// </summary>
        Written,
        /// <summary>
        /// The write has been cancelled.
        /// </summary>
        Cancelled
    }
}
