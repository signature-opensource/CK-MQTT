using CK.MQTT.Packets;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Represent a MQTT3 Client.
    /// </summary>
    public interface IConnectedMessageExchanger : IAsyncDisposable
    {
        /// <summary>
        /// Disconnect the client.
        /// Once the client is successfully disconnected, the <see cref="Disconnected"/> event will be fired
        /// with the <see cref="DisconnectReason.UserDisconnected"/>.
        /// </summary>
        /// <returns>True if this call actually closed the connection, false if the connection has already been closed by a concurrent decision.</returns>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180903">MQTT Disconnect</a>
        /// for more details about the protocol disconnection
        /// </remarks>
        Task<bool> DisconnectAsync( bool deleteSession );

        /// <summary>
        /// Publish an <see cref="OutgoingMessage"/>.
        /// </summary>
        /// <param name="message">The message to publish.</param>
        /// <returns>
        ///A <see cref="ValueTask{TResult}"/> that complete when the publish is guaranteed to be sent.
        ///The <see cref="Task{T}"/> complete when the client received an ack for this publish.
        /// </returns>
        ValueTask<Task> PublishAsync( OutgoingMessage message );
    }
}
