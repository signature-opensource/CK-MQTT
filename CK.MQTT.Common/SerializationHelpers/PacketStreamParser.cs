using CK.Core;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Serialisation
{
    public static class PacketStreamParser
    {
        static SequenceReadResult TryReadPacketFixedHeader( ref ReadResult readResult, out byte header, out int length, out SequencePosition consumed )
        {
            SequenceParser<byte> reader = new SequenceParser<byte>( readResult.Buffer );
            bool headerSuccess = reader.TryRead( out header );
            SequenceReadResult result = reader.TryReadRemainingLength( out length );
            consumed = reader.Position;
            if( headerSuccess && result == SequenceReadResult.Ok ) return SequenceReadResult.Ok;
            if( !headerSuccess || result == SequenceReadResult.NotEnoughBytes ) return SequenceReadResult.NotEnoughBytes;
            return SequenceReadResult.CorruptedStream;
        }

        static async ValueTask<(byte header, int length)?> TryReadPacketFixedHeaderAsync( IActivityMonitor m, PipeReader pipeReader, CancellationToken cancellationToken )
        {
            while( true )
            {
                ReadResult readResult = await pipeReader.ReadAsync( cancellationToken );
                if( readResult.IsCanceled || cancellationToken.IsCancellationRequested )
                {
                    m.Error( "Read cancelled." );
                    return (0, 0);
                }
                SequenceReadResult sequenceRead = TryReadPacketFixedHeader( ref readResult, out byte header, out int length, out SequencePosition consumed );
                if( sequenceRead == SequenceReadResult.Ok )
                {
                    pipeReader.AdvanceTo( consumed );
                    return (header, length);
                }
                if( sequenceRead == SequenceReadResult.CorruptedStream ) return null;
                if( sequenceRead != SequenceReadResult.NotEnoughBytes ) throw new InvalidOperationException();
                if( readResult.IsCompleted )
                {
                    if( readResult.Buffer.Length != 0 )
                    {
                        m.Error( "Unexpected End Of Stream while reading packet header." );
                    }
                    else
                    {
                        m.Trace( "Pipe completed, aborting reading next packet due to ." );
                    }
                    return null;
                }
            }
        }

        static bool IsACopyOnlyPacket( PacketType packetType )
            => packetType != PacketType.Connect && packetType != PacketType.Publish;

        static IPacket? UnknownPacketType( IActivityMonitor m )
        {
            m.Error( "Unknown packet type" );
            return null;
        }

        static IPacket? DeserialisePacket(
            IActivityMonitor m,
            PacketType packetType,
            byte header,
            ReadOnlySequence<byte> packetBuffer
            ) => packetType switch
            {
                PacketType.ConnectAck => ConnectAck.Deserialize( m, packetBuffer ),
                PacketType.PublishAck => PublishAck.Deserialize( m, packetBuffer ),
                PacketType.PublishReceived => PublishReceived.Deserialize( m, packetBuffer ),
                PacketType.PublishRelease => PublishRelease.Deserialize( m, packetBuffer ),
                PacketType.PublishComplete => PublishComplete.Deserialize( m, packetBuffer ),
                PacketType.Subscribe => Subscribe.Deserialize( m, packetBuffer ),
                PacketType.SubscribeAck => SubscribeAck.Deserialize( m, packetBuffer ),
                PacketType.Unsubscribe => Unsubscribe.Deserialize( m, packetBuffer ),
                PacketType.UnsubscribeAck => UnsubscribeAck.Deserialize( m, packetBuffer ),
                PacketType.PingRequest => PingRequest.Deserialize( m, packetBuffer ),
                PacketType.PingResponse => PingResponse.Deserialize( m, packetBuffer ),
                PacketType.Disconnect => OutgoingDisconnect.Deserialize( m, packetBuffer ),
                PacketType.Connect => Connect.Deserialize( m, buffer, false ),
                PacketType.Publish => Publish.Deserialise( m, header, buffer ),
                _ => UnknownPacketType( m ),
            };

        /// <summary>
        /// Parse a packet. Return <see langword="null"/> on error.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="memoryBuffer">The buffer to use to parse the fixed header.</param>
        /// <param name="stream">The channel stream to use.</param>
        /// <param name="cancellationToken">The cancellation token to cancel IO operations.</param>
        /// <returns><see langword="null"/> on error, the packet otherwise.</returns>
        public static async ValueTask<IPacket?> ReadPacket( IActivityMonitor m, PipeReader pipeReader,  long giveUpToStreamTreshold, CancellationToken cancellationToken )
        {
            var res = await TryReadPacketFixedHeaderAsync( m, pipeReader, cancellationToken );
            if( res == null ) return null;
            (byte header, int packetSize) = res.Value;
            PacketType packetType = (PacketType)((byte)PacketType.Mask & header);
            bool copyOnlyPacket = IsACopyOnlyPacket( packetType );
            IPacket? packet;
            if( packetSize == 0 )
            {
                DeserialisePacket( m, packetType, header, ReadOnlySequence<byte>.Empty );
            }
            if( packetSize >= giveUpToStreamTreshold ) throw new NotImplementedException();
            m.Trace( $"Incoming Packet is below the treshold({giveUpToStreamTreshold}bytes) and will be loaded in memory." );
            ReadResult readResult;
            do
            {
                readResult = await pipeReader.ReadAsync();
                m.Trace( $"Received {readResult.Buffer.Length}/{packetSize} bytes." );
                if(cancellationToken.IsCancellationRequested || readResult.IsCanceled)
                {
                    m.Warn( "Packet reading canceled." );
                }
                if(readResult.IsCompleted)
                {
                    m.Error( "Unexpected End Of Stream while receiving an incoming packet." );
                }
            } while( readResult.Buffer.Length < packetSize );
            ReadOnlySequence<byte> sequence = readResult.Buffer.Slice( 0, packetSize );
            pipeReader.AdvanceTo( sequence.End );
            return DeserialisePacket( m, packetType, header, sequence );
        }
    }
}
