using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Represent a MQTT3 Client.
    /// </summary>
    public interface IMqttClient
    {
        /// <summary>
        /// <see langword="delegate"/> used when the client is Disconnected.
        /// </summary>
        /// <param name="m">The logger, use it to log the activities perfomed while processing the disconnection.</param>
        /// <param name="arg">Object containing information about the disconnection, like the reason.</param>
        public delegate void Disconnected( IMqttLogger m, MqttEndpointDisconnected arg );

        /// <summary>
        /// <see langword="delegate"/> called when the <see cref="IMqttClient"/> got Disconnected.
        /// See <see cref="MqttEndpointDisconnected"/> for more details on the disconnection information.
        /// </summary>
        Disconnected? DisconnectedHandler { get; set; }

        /// <summary>
        /// Return <see langword="false"/> if the last operation on the underlying Communication Channel was not successfull.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// <see langword="delegate"/> called when the client receive an <see cref="IncomingMessage"/>.
        /// </summary>
        /// <param name="monitor">The logger, use it to log the activities perfomed while processing the <see cref="IncomingMessage"/>.</param>
        /// <param name="message">The message to process. You MUST read completly the message, or you will corrupt the communication stream !</param>
        /// <returns>A <see cref="ValueTask"/> that complete when the packet processing is finished.</returns>
        public delegate ValueTask MessageHandlerDelegate( IMqttLogger monitor, IncomingMessage message );

        /// <summary>
        /// <see langword="delegate"/> called when the <see cref="IMqttClient"/> receive a Publish Packet.
        /// </summary>
        MessageHandlerDelegate? MessageHandler { get; set; }

        /// <summary>
        /// Connect the <see cref="IMqttClient"/> to a Broker.
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
        Task<ConnectResult> ConnectAsync( IMqttLogger m, MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null );

        /// <summary>
        /// Susbscribe the <see cref="IMqttClient"/> to a <a href="docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Ref374621403">Topic</a>.
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
        ValueTask<Task<SubscribeReturnCode[]?>> SubscribeAsync( IMqttLogger m, params Subscription[] subscriptions );

        /// <summary>
        /// Send a message to the broker.
        /// </summary>
        /// <param name="m">The logger used to log the activities about the process of sending the message.</param>
        /// <param name="message">
        /// The application message to publish to the Server.
        /// See <see cref="OutgoingApplicationMessage" /> for more details about the application messages
        /// </param>
        /// <returns>
        /// The <see cref="ValueTask{TResult}"/> that complete when the publish is guaranteed to be sent.
        /// The <see cref="Task"/> complete when the client receive the broker acknowledgement.
        /// </returns>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180850">MQTT Publish</a>
        /// for more details about the protocol publish
        /// </remarks>
        ValueTask<Task> PublishAsync( IMqttLogger m, OutgoingApplicationMessage message );

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
        ValueTask<Task> UnsubscribeAsync( IMqttLogger m, params string[] topics );

        /// <summary>
        /// Disconnect the client.
        /// Once the client is successfully disconnected, the <see cref="Disconnected"/> event will be fired 
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> that complete when the client is disconnected.</returns>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180903">MQTT Disconnect</a>
        /// for more details about the protocol disconnection
        /// </remarks>
        ValueTask DisconnectAsync( IMqttLogger m );
    }

}
