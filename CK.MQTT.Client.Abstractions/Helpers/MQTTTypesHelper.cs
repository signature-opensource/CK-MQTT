using System.Text;

namespace CK.MQTT
{
    public static class MQTTTypesHelper
    {
        /// <summary>
        /// Gets the serialized size in bytes of the given <see cref="string"/>.
        /// </summary>
        /// <param name="mqttString">The string to compute.</param>
        /// <returns>The serialized size in bytes of the input <see cref="string"/>.</returns>
        public static int MQTTSize( this string mqttString ) => 2 + Encoding.UTF8.GetByteCount( mqttString );

        public static int CompactByteCount( this int packetLength )
        {
            int i = 0;
            while( packetLength >= 0x80 )
            {
                i++;
                packetLength >>= 7;
            }
            return i + 1;
        }
    }
}
