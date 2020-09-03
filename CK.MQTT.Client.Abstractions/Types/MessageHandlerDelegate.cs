using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// <see langword="delegate"/> called when the client receive an <see cref="IncomingMessage"/>.
    /// </summary>
    /// <param name="message">The message to process. You MUST read completly the message, or you will corrupt the communication stream !</param>
    /// <returns>A <see cref="ValueTask"/> that complete when the packet processing is finished.</returns>
    public delegate ValueTask MessageHandlerDelegate( IncomingMessage message );
}
