using CK.Core;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Stores;
using System;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Processes
{
    public static class PublishSenderProcesses
    {
        /// <summary>
        /// Execute the publish protocol with the given QoS.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="channel">The channel used to send and receive messages.</param>
        /// <param name="messageStore">The message store to use to store messages.</param>
        /// <param name="topic">The topic of the message to send.</param>
        /// <param name="payload">The payload of the message to send.</param>
        /// <param name="qos">The QoS of the message to send.</param>
        /// <param name="retain">The retain flag that we will send in the packet.</param>
        /// <returns>A <see cref="Task{Task}"/> that complete when the guarantee of the QoS is fulfilled.
        /// The result of this <see cref="Task"/> that complete when the publish process is completed. </returns>
        public static ValueTask<ValueTask> Publish(
            IActivityMonitor m,
            IMqttChannel<IPacket> channel,
            IPacketStore messageStore,
            string topic, ReadOnlyMemory<byte> payload, QualityOfService qos, bool retain,
            int waitTimeoutMs )
            => qos switch
            {
                QualityOfService.AtMostOnce => new ValueTask<ValueTask>( PublishQoS0( m, channel, topic, payload, retain ) ),
                QualityOfService.AtLeastOnce => PublishQoS1( m, channel, messageStore, topic, payload, retain, waitTimeoutMs ),
                QualityOfService.ExactlyOnce => PublishQoS2( m, channel, messageStore, topic, payload, retain, waitTimeoutMs ),
                _ => throw new ArgumentException( "Invalid QoS." ),
            };

        public static ValueTask PublishQoS0( IActivityMonitor m, IMqttChannel<IPacket> channel,
            string topic, ReadOnlyMemory<byte> payload, bool retain )//payload args.
        {
            using( m.OpenTrace( "Executing Publish protocol with QoS 0." ) )
            {
                return channel.SendAsync( m, new Publish( topic, payload, retain, false ), default );
            }
        }

        public static async ValueTask<ValueTask> PublishQoS1( IActivityMonitor m, IMqttChannel<IPacket> channel, IPacketStore messageStore,
            string topic, ReadOnlyMemory<byte> payload, bool retain, //payload args.
            int waitTimeoutMs )
        {
            using( m.OpenTrace( "Executing Publish protocol with QoS 1." ) )
            {
                // TODO: we got an useless allocation there. The stored object could be the same than the sent one.
                // The issue is that the state of the object should change (packetID, dup flag)
                ApplicationMessage packet = new ApplicationMessage( topic, payload );
                ushort pacektId = await messageStore.StoreMessageAsync( m, packet, QualityOfService.AtLeastOnce );//store the message
                //Now we can guarantee the At Least Once, the message have been stored.
                //We return the Task representing the rest of the protocol.
                return PublishQoS1SendPub( m, channel, messageStore, topic, payload, retain, pacektId, waitTimeoutMs );
            }
        }

        public static async ValueTask PublishQoS1SendPub(
            IActivityMonitor m, IMqttChannel<IPacket> channel, IPacketStore messageStore,
            string topic, ReadOnlyMemory<byte> payload, bool retain, ushort packetId,  //payload args.
            int waitTimeoutMs )
        {
            PublishAck ack;
            using( m.OpenTrace( "Sending Publish packet to Receiver." ) )
            {
                Publish publish = new PublishWithId( topic, payload, QualityOfService.AtLeastOnce, retain, false, packetId );
                ack = await channel.SendAndWaitResponseWithRetries<IPacket, PublishAck, Publish>( m, publish,
                    //match packet of same type, with same packetId.
                    ( p ) => p.PacketId == packetId, waitTimeoutMs,
                    t => { t.Duplicated = true; return t; } );
                if( !(ack.PacketId != packetId) ) throw new InvalidOperationException( "This code path should never be reached." );
            }
            await OnPubAckReceived( m, ack, messageStore );
        }

        public static async ValueTask OnPubAckReceived( IActivityMonitor m, PublishAck ack, IPacketStore messageStore )
        {
            //We can discard the stored ID.
            QualityOfService qos = await messageStore.DiscardMessageFromIdAsync( m, ack.PacketId );
            if( qos != QualityOfService.AtLeastOnce ) throw new InvalidOperationException( $"Stored message had QoS of {qos} but was expecting {QualityOfService.AtLeastOnce}" );
        }

        public static async ValueTask<ValueTask> PublishQoS2( IActivityMonitor m, IMqttChannel<IPacket> channel, IPacketStore messageStore,
            string topic, ReadOnlyMemory<byte> payload, bool retain, //payload args.
            int waitTimeoutMs )
        {
            // TODO: we got an useless allocation there. The stored object could be the same than the sent one.
            // The issue is that the state of the object should change (packetID, dup flag)
            ApplicationMessage packet = new ApplicationMessage( topic, payload );
            ushort packetId = await messageStore.StoreMessageAsync( m, packet, QualityOfService.ExactlyOnce );//store the message
            //Now we can guarantee the At Least Once, the message have been stored.
            //We return the Task representing the rest of the protocol.
            return PublishQoS2SendPub( m, channel, messageStore, topic, payload, retain, packetId, waitTimeoutMs );
        }

        public static async ValueTask PublishQoS2SendPub( IActivityMonitor m, IMqttChannel<IPacket> channel, IPacketStore messageStore,
            string topic, ReadOnlyMemory<byte> payload, bool retain, ushort packetId,  //payload args.
            int waitTimeoutMs )
        {
            Publish publish = new PublishWithId( topic, payload, QualityOfService.ExactlyOnce, retain, false, packetId );
            PublishReceived ack = await channel.SendAndWaitResponseWithRetries<IPacket, PublishReceived, Publish>( m, publish,
                //match packet of same type, with same packetId.
                ( p ) => p is PublishReceived a && a.PacketId == packetId, waitTimeoutMs,
                t => { t.Duplicated = true; return t; } );
            if( !(ack.PacketId != packetId) ) throw new InvalidOperationException( "This code path should never be reached." );
            await PublishQoS2OnPubRecReceived( m, channel, messageStore, ack, waitTimeoutMs );
        }

        public static async ValueTask PublishQoS2OnPubRecReceived( IActivityMonitor m, IMqttChannel<IPacket> channel, IPacketStore messageStore,
            PublishReceived publishReceived,
            int waitTimeoutMs )
        {
            QualityOfService qos = await messageStore.DiscardMessageFromIdAsync( m, publishReceived.PacketId );
            if( qos != QualityOfService.ExactlyOnce ) throw new InvalidOperationException( $"Stored message had QoS of {qos} but was expecting {QualityOfService.ExactlyOnce}" );
            await PublishQoS2SendPubRel( m, channel, messageStore, publishReceived.PacketId, waitTimeoutMs );
        }

        public static async ValueTask PublishQoS2SendPubRel( IActivityMonitor m, IMqttChannel<IPacket> channel, IPacketStore messageStore,
            ushort packetId,
            int waitTimeoutMs )
        {
            PublishRelease pubRel = new PublishRelease( packetId );
            PublishComplete pubComp = await channel.SendAndWaitResponseWithRetries<IPacket, PublishComplete, PublishRelease>( m, pubRel,
                p => p.PacketId == packetId,
                waitTimeoutMs );
            await OnPubCompReceived( m, messageStore, pubComp );
        }

        public static async ValueTask OnPubCompReceived( IActivityMonitor m, IPacketStore messageStore, PublishComplete publishComplete )
            => await messageStore.FreePacketIdAsync( m, publishComplete.PacketId );
    }
}
