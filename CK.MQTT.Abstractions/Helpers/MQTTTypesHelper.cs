using System;
using System.Text;

namespace CK.MQTT
{
    /// <summary>
    /// Various extensions methods on base types to help serializing mqtt packets.
    /// </summary>
    public static class MQTTTypesHelper
    {
        /// <summary>
        /// Gets the serialized size in bytes of the given <see cref="string"/>.
        /// </summary>
        /// <param name="mqttString">The string to compute.</param>
        /// <remarks>
        /// See also <a href="https://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_UTF-8_encoded_strings_"> mqtt string specification</a>.
        /// </remarks>
        /// <returns>The serialized size in bytes of the input <see cref="string"/>.</returns>
        public static int MQTTSize( this string mqttString ) => 2 + Encoding.UTF8.GetByteCount( mqttString );

        /// <summary>
        /// Get the number of bytes required to express the remaining size of a mqtt packet.
        /// </summary>
        /// <param name="packetLength">The length of a packet.</param>
        /// <remarks>
        /// See also <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc442180836">Remaining length specification</a>.
        /// </remarks>
        /// <returns>The amount of byte required to serialize the given length.</returns>
        public static int CompactByteCount( this int packetLength )
        {
            int i = 0;
            while( packetLength >= 0x80 ) //TODO: i bet there is a way to do this without a loop.
            {
                i++;
                packetLength >>= 7;
            }
            return i + 1;
        }

        public static int MQTTSize( this ReadOnlyMemory<byte> buffer ) => 2 + buffer.Length;
    }
}
