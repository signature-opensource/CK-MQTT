using CK.Core;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Serialisation
{
    public static class PacketStreamParser
    {
        static async ValueTask<(byte header, int packetSize, ReadOnlyMemory<byte> readBytes, bool success)> ReadPacketFixedHead(
            IActivityMonitor m,
            Memory<byte> memoryBuffer,
            int alreadyReadCount,
            IGenericChannelStream stream,
            CancellationToken cancellationToken
            )
        {
            Debug.Assert( memoryBuffer.Length >= 5 );
            bool CheckCancel()
            {
                bool isCancel = cancellationToken.IsCancellationRequested;
                if( isCancel )
                {
                    m.Trace( "ReadPacket cancelled." );
                }
                return isCancel;
            }
            async ValueTask<bool> ReadMoreBytes()
            {
                if( CheckCancel() ) return false;
                int readCount = await stream.ReadAsync( memoryBuffer, cancellationToken );
                if( CheckCancel() ) return false;
                alreadyReadCount += readCount;
                if( readCount != 0 ) return true;
                m.Error( "Unexpected End Of Stream." );
                return false;
            }
            if( alreadyReadCount == 0 && !await ReadMoreBytes() ) return (0, 0, memoryBuffer, false);
            byte header = memoryBuffer.Span[0];
            PacketType packetType = (PacketType)(header & (byte)PacketType.Mask);
            //Read can read only one byte. If the sender send only one byte for exemple...
            if( alreadyReadCount == 1 && !await ReadMoreBytes() ) return (header, 0, memoryBuffer, false);//EOS or Cancelled.
            alreadyReadCount--;
            memoryBuffer = memoryBuffer[1..];//First byte is parsed, we truncate the buffer to ignore it now.

            byte byteReadCount = MqttBinaryReader.ReadRemainingLength( memoryBuffer[..alreadyReadCount].Span, out int packetSize );
            bool CheckResult()
            {
                bool error = packetSize == -2;
                if( error )
                {
                    m.Error( "Length field of the packet is corrupted. Invalid packet or corrupted stream." );
                }
                return !error;
            }
            if( !CheckResult() ) return (header, 0, memoryBuffer, false);
            while( packetSize == -1 )
            {
                if( !await ReadMoreBytes() ) return (header, 0, memoryBuffer, false);
                byteReadCount = MqttBinaryReader.ReadRemainingLength( memoryBuffer[..alreadyReadCount].Span, out packetSize );
                if( !CheckResult() ) return (header, 0, memoryBuffer, false);
            }
            memoryBuffer = memoryBuffer[byteReadCount..alreadyReadCount];
            return (header, packetSize, memoryBuffer, true);
        }

        static bool IsACopyOnlyPacket( PacketType packetType )
            => packetType != PacketType.Connect && packetType != PacketType.Publish;

        static IPacket? UnknownPacketType( IActivityMonitor m )
        {
            m.Error( "Unknown packet type" );
            return null;
        }

        static IPacket? DeserialiseCopyOnlyPacket(
            IActivityMonitor m,
            PacketType packetType,
            ReadOnlySpan<byte> packetBuffer
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
                PacketType.Disconnect => Disconnect.Deserialize( m, packetBuffer ),
                _ => UnknownPacketType( m ),
            };

        static IPacket? DeserialisePacketRequiringAlloc(
            IActivityMonitor m, PacketType packetType, byte header,
            ReadOnlyMemory<byte> buffer
        )
        {
            return packetType switch
            {
                PacketType.Connect => Connect.Deserialize( m, buffer, false ),
                PacketType.Publish => Publish.Deserialise( m, header, buffer ),
                _ => UnknownPacketType( m ),
            };
        }

        /// <summary>
        /// Parse a packet. Return <see langword="null"/> on error.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="memoryBuffer">The buffer to use to parse the fixed header.</param>
        /// <param name="stream">The channel stream to use.</param>
        /// <param name="cancellationToken">The cancellation token to cancel IO operations.</param>
        /// <returns><see langword="null"/> on error, the packet otherwise.</returns>
        public static async ValueTask<(IPacket?, ReadOnlyMemory<byte>, IDisposable?)> ReadPacket(
            IActivityMonitor m,
            Memory<byte> memoryBuffer,
            int alreadyReadCount,
            IGenericChannelStream stream,
            CancellationToken cancellationToken )
        {
            byte header;
            int packetSize;
            ReadOnlyMemory<byte> alreadyReadBytes;
            using( var grp = m.OpenDebug( "Reading incoming packet fixed header." ) )
            {
                bool success;
                (header, packetSize, alreadyReadBytes, success) = await ReadPacketFixedHead( m, memoryBuffer, alreadyReadCount, stream, cancellationToken );
                grp.ConcludeWith( () => success ? "Success." : "Error." );
                if( !success ) return (null, memoryBuffer, null);
            }
            PacketType packetType = (PacketType)((byte)PacketType.Mask & header);
            bool copyOnlyPacket = IsACopyOnlyPacket( packetType );
            bool packetFullyInBuffer = packetSize <= alreadyReadBytes.Length;
            ReadOnlyMemory<byte> nextPacketBuffer = packetFullyInBuffer ?
                  alreadyReadBytes[packetSize..] //Some packet size 2 bytes, so we save the extra bytes.
                : ReadOnlyMemory<byte>.Empty;  //Resizing this buffer so it contain only the bytes of the next packet.
            alreadyReadBytes = alreadyReadBytes[..packetSize];//Resizing this buffer so it contain only the packet data.
            IPacket? packet;
            if( packetFullyInBuffer )//No alloc required path!
            {
                if( copyOnlyPacket )
                {
                    packet = DeserialiseCopyOnlyPacket( m, packetType, alreadyReadBytes.Span );
                    return (packet, nextPacketBuffer, null);
                }
                if( packetSize == 0 )
                {
                    DeserialisePacketRequiringAlloc( m, packetType, header, ReadOnlyMemory<byte>.Empty );
                }
            }

            Memory<byte> packetMemory = Memory<byte>.Empty;
            // MemoryPool can rent more bytes that we need.
            IMemoryOwner<byte> rentedMemory = MemoryPool<byte>.Shared.Rent( packetSize );
            // Because we rely on the buffer length, we truncate it so it got the correct length.
            packetMemory = rentedMemory.Memory[..packetSize];
            using( m.OpenDebug( "Reading incoming packet content..." ) )
            {
                alreadyReadBytes.CopyTo( packetMemory );//We may already have read bytes of the payload. So we copy them in the 
                int i = alreadyReadBytes.Length;
                int max = packetMemory.Length;
                while( i != max )
                {
                    int readCount = await stream.ReadAsync( packetMemory[i..], cancellationToken );
                    if( cancellationToken.IsCancellationRequested )
                    {
                        m.Error( "Operation Cancelled." );
                        rentedMemory.Dispose();
                        return (null, memoryBuffer, rentedMemory);
                    }
                    if( readCount == 0 )
                    {
                        m.Error( "Unexpected End Of Stream." );
                        rentedMemory.Dispose();
                        return (null, memoryBuffer, rentedMemory);
                    }
                    i += readCount;
                }
            }

            using( m.OpenTrace( $"Deserialising incoming packet: type '{packetType}', size '{packetSize}' bytes (size without fixed header)." ) )
            {
                if( copyOnlyPacket )
                {
                    packet = DeserialiseCopyOnlyPacket( m, packetType, packetMemory.Span );
                    rentedMemory.Dispose();//Here we copy only, no handle on the stream are kept.
                    return (packet, nextPacketBuffer, null);
                }
                packet = DeserialisePacketRequiringAlloc( m, packetType, header, packetMemory );
                return (packet, nextPacketBuffer, rentedMemory);
            }
        }
    }
}
