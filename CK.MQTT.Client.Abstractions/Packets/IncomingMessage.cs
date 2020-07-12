using System.IO.Pipelines;

namespace CK.MQTT
{
    /// <summary>
    /// Represent an incoming publish message.
    /// </summary>
    public class IncomingMessage
    {
        /// <summary>
        /// Instantiate an <see cref="IncomingMessage"/>.
        /// </summary>
        /// <param name="topic">The topic.</param>
        /// <param name="pipeReader">The <see cref="PipeReader"/>.</param>
        /// <param name="duplicate">Whether it's a duplicate packet or not.</param>
        /// <param name="retain">Retain flag.</param>
        /// <param name="payloadLength">The length of the payload.</param>
        public IncomingMessage( string topic, PipeReader pipeReader, bool duplicate, bool retain, int payloadLength )
        {
            Topic = topic;
            PipeReader = pipeReader;
            Duplicate = duplicate;
            Retain = retain;
            PayloadLength = payloadLength;
        }

        /// <summary>
        /// The topic of the message.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// The pipereader.
        /// You must read all the message or will corrupt the reading (and result in a Protocol error).
        /// </summary>
        public PipeReader PipeReader { get; }

        /// <summary>
        /// If <see langword="false"/>, it indicates that this is the first occasion that the Client or Server has attempted to send this Packet.
        /// <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349262">See the specification for more details</a>.
        /// </summary>
        public bool Duplicate { get; }

        /// <summary>
        /// If you are a Client receiving the message,
        ///     this flag signal that the packet was retained on the server and was sent as the result of a subscription.<br/>
        /// If you are a Server receiving the message, this flag signal that the Client ask for this packet to be retained.<br/>
        /// <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349265">See the specification for more details</a>.
        /// </summary>
        public bool Retain { get; }

        /// <summary>
        /// The length of the payload. Usefull to know how many data you must read on the <see cref="PipeReader"/>.
        /// </summary>
        public int PayloadLength { get; }
    }
}
