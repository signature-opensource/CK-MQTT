using CK.Core;
using System;
using System.IO.Pipelines;
using System.Threading;
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

        void SetMessageHandler( Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler );

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
        /// with the <see cref="DisconnectedReason.UserDisconnected"/>.
        /// </summary>
        /// <returns>True if this call actually closed the connection, false if the connection has already been closed by a concurrent decision.</returns>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180903">MQTT Disconnect</a>
        /// for more details about the protocol disconnection
        /// </remarks>
        Task<bool> DisconnectAsync( IActivityMonitor m, bool deleteSession, bool cancelAckTasks );
    }

}
