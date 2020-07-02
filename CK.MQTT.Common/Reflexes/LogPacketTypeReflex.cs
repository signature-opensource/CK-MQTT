using CK.Core;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Reflexes
{
    public class LogPacketTypeReflex
    {
        public static ValueTask ProcessIncomingPacket( IActivityMonitor m, IncomingMessageHandler sender, byte header, int packetSize, PipeReader reader, Func<ValueTask> next )
        {
            m.Trace( $"Packet Type is {(PacketType)((header >> 4) << 4)}." );
            return next();
        }
    }
}
