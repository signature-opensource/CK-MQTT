using CK.Core;
using System;

namespace CK.MQTT
{
    /// <summary>
    /// Simple wrapper of an <see cref="IActivityMonitor"/> to a <see cref="IOutputLogger"/>.
    /// </summary>
    public class OutputLoggerMqttActivityMonitor : IOutputLogger
    {
        readonly IActivityMonitor _m;

        /// <summary>
        /// Instantiate this wrapper.
        /// </summary>
        /// <param name="m">The <see cref="IActivityMonitor"/> to wrap.</param>
        public OutputLoggerMqttActivityMonitor( IActivityMonitor m )
        {
            _m = m;
        }

        public void ExceptionInOutputLoop( Exception e ) => _m.Error()?.Send( e, "Error while writing data." );

        public IDisposable? OutputLoopStarting() => _m.OpenTrace()?.Send( "Output loop listening..." );

        public void PacketMarkedPoisoned( int packetId, int tryCount )
            => _m.Error()?.Send( $"Packet with id {packetId} is not acknowledged after sending it {tryCount} times." +
                        $"\nThis was the last attempt, as configured." );

        public void SendingKeepAlive() => _m.Trace().Send( "Sending PingReq." );

        public IDisposable? SendingMessage( ref IOutgoingPacket outgoingPacket, ProtocolLevel protocolLevel )
            => _m.OpenInfo()?.Send( $"Sending message '{outgoingPacket}' of size {outgoingPacket.GetSize( protocolLevel )}." );

        public IDisposable? SendingMessageWithId( ref IOutgoingPacket outgoingPacket, ProtocolLevel protocolLevel, int packetId )
            => _m.OpenInfo()?.Send( $"Sending message '{outgoingPacket}' of size {outgoingPacket.GetSize( protocolLevel )} with packet ID {packetId}." );
    }
}
