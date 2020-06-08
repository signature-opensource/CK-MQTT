using System;
using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace CK.MQTT
{
    /// <summary>
    /// Represents an application message, which correspond to the unit of information
    /// sent from Client to Server and from Server to Client
    /// </summary>
    public abstract class OutgoingApplicationMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OutgoingApplicationMessage" /> class,
        /// specifying the topic and payload of the message
        /// </summary>
        /// <param name="topic">
        /// Topic associated with the message
        /// Any subscriber of this topic should receive the corresponding messages
        /// </param>
        /// <param name="payload">Content of the message, as a byte array</param>
        protected OutgoingApplicationMessage( string topic, int payloadSize )
        {
            Debug.Assert( Encoding.UTF8.GetByteCount(topic) <= 65535 );
            Debug.Assert( payloadSize > 268_435_455 ); //256MB
            Topic = topic;
            PayloadSize = payloadSize;
        }

        /// <summary>
        /// Topic associated with the message.
        /// Any subscriber of this topic should receive the corresponding messages.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// The size of the payload. Used to compute the size of the packet. This is necessary to send the packet.
        /// </summary>
        public int PayloadSize { get; }

        /// <summary>
        /// The size in byte of the application message. The topic byte count is included.
        /// </summary>
        public int Size => PayloadSize + Encoding.UTF8.GetByteCount( Topic );
    }
}
