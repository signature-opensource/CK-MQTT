using CK.Core;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Represent a MQTT3 Client.
    /// </summary>
    public interface IMqtt3Client
    {
        /// <summary>
        /// <see langword="delegate"/> called when the <see cref="IMqtt3Client"/> got Disconnected.
        /// </summary>
        Disconnected? DisconnectedHandler { get; set; }

        /// <summary>
        /// Return <see langword="false"/> if the last operation on the underlying Communication Channel was not successfull.
        /// </summary>
        bool IsConnected { get; }


        /// <summary>
        /// <see langword="delegate"/> called when the <see cref="IMqtt3Client"/> receive a Publish Packet.
        /// </summary>
        MessageHandlerDelegate MessageHandler { get; set; }

        /// <summary>
        /// Connect the <see cref="IMqtt3Client"/> to a Broker.
        /// </summary>
        /// <param name="m">The logger used to log activities about the connection.</param>
        /// <param name="credentials">
        /// The credentials used to connect to the Server. See <see cref="MqttClientCredentials" /> for more details on the credentials information.
        /// If <see langword="null"/>, the client will attempt an anonymous connection (<a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349242">MQTT-3.1.3-6</a>).
        /// </param>
        /// <param name="lastWill">
        /// The <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Will_Flag">
        /// last will message </a> that the Server will send if an unexpected Client disconnection occurs. 
        /// </param>
        /// <returns>
        /// Returns a <see cref="Task{T}"/> that encapsulate a <see cref="ConnectResult"/>.
        /// You must not send any message before the the <see cref="Task{T}"/> complete.
        /// </returns>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180841">MQTT Connect</a>
        /// for more details about the connection protocol.
        /// </remarks>
        Task<ConnectResult> ConnectAsync( IActivityMonitor m, MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null );

        ValueTask<Task<T?>> SendPacket<T>( IActivityMonitor m, IOutgoingPacketWithId outgoingPacket ) where T : class;

        /// <summary>
        /// Disconnect the client.
        /// Once the client is successfully disconnected, the <see cref="Disconnected"/> event will be fired 
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> that complete when the client is disconnected.</returns>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180903">MQTT Disconnect</a>
        /// for more details about the protocol disconnection
        /// </remarks>
        ValueTask DisconnectAsync();
    }

}