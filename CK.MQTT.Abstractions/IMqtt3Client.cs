using CK.MQTT.Packets;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Represent a MQTT3 Client.
    /// </summary>
    public interface IMqtt3Client : IDisposable
    {
        /// <summary>
        /// Return <see langword="false"/> if the last operation on the underlying Communication Channel was not successfull.
        /// </summary>
        bool IsConnected { get; }

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
        Task<ConnectResult> ConnectAsync( OutgoingLastWill? lastWill = null, CancellationToken cancellationToken = default );

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
        Task<bool> DisconnectAsync( bool deleteSession, bool cancelAckTasks, CancellationToken cancellationToken );

        /// <summary>
        /// Unsubscribe the client from topics.
        /// </summary>
        /// <param name="m">The logger used to log the activities about the unsubscribe process.</param>
        /// <param name="topics">
        /// The list of topics to unsubscribe from.
        /// </param>
        /// <returns>
        /// The <see cref="ValueTask{TResult}"/> that complete when the Unsubscribe is guaranteed to be sent.
        /// The <see cref="Task"/> complete when the client receive the broker acknowledgement.
        /// Once the Task completes, no more application messages for those topics will arrive.</returns>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180885">MQTT Unsubscribe</a>
        /// for more details about the protocol unsubscription
        /// </remarks>
        ValueTask<Task> UnsubscribeAsync( params string[] topics );

        /// <summary>
        /// Susbscribe the <see cref="IMqtt3Client"/> to multiples <a href="docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Ref374621403">Topic</a>.
        /// </summary>
        /// <param name="m">The logger used to log the activities about the subscription process.</param>
        /// <param name="subscriptions">The subscriptions to send to the broker.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that complete when the subscribe is guaranteed to be sent.
        /// The <see cref="Task{T}"/> complete when the client received the <a href="docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc384800441">Subscribe acknowledgement</a>.
        /// It's Task result contain a <see cref="SubscribeReturnCode"/> per subcription, with the same order than the array given in parameters.
        /// </returns>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180876">MQTT Subscribe</a>
        /// for more details about the protocol subscription.
        /// </remarks>
        ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( IEnumerable<Subscription> subscriptions );

        /// <summary>
        /// Susbscribe the <see cref="IMqtt3Client"/> to a <a href="docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Ref374621403">Topic</a>.
        /// </summary>
        /// <param name="m">The logger used to log the activities about the subscription process.</param>
        /// <param name="subscriptions">The subscriptions to send to the broker.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that complete when the subscribe is guaranteed to be sent.
        /// The <see cref="Task{T}"/> complete when the client received the <a href="docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc384800441">Subscribe acknowledgement</a>.
        /// It's Task result contain a <see cref="SubscribeReturnCode"/> per subcription, with the same order than the array given in parameters.
        /// </returns>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180876">MQTT Subscribe</a>
        /// for more details about the protocol subscription.
        /// </remarks>
        ValueTask<Task<SubscribeReturnCode>> SubscribeAsync( Subscription subscriptions );

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
