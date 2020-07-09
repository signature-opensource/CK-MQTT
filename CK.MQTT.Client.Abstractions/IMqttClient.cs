using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Represent a MQTT Client.
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
        /// Returns a <see cref="Task{ConnectResult}"/> that encapsulate a <see cref="ConnectResult"/>.
        /// You must not send any message before the the <see cref="Task{ConnectResult}"/> complete.
        /// </returns>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180841">MQTT Connect</a>
        /// for more details about the connection protocol.
        /// </remarks>
        Task<ConnectResult> ConnectAsync( IMqttLogger m, MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null );

        /// <summary>
        /// Represents the protocol subscription, which consists of sending a SUBSCRIBE packet
        /// and awaiting the corresponding SUBACK packet from the Server
        /// </summary>
        /// <param name="topicFilter">
        /// The topic to subscribe for incoming application messages. 
        /// Every message sent by the Server that matches a subscribed topic, will go to the <see cref="MessageStream"/> 
        /// </param>
        /// <param name="qos">
        /// The maximum Quality Of Service (QoS) that the Server should maintain when publishing application messages for the subscribed topic to the Client
        /// This QoS is maximum because it depends on the QoS supported by the Server. 
        /// See <see cref="QualityOfService" /> for more details about the QoS values
        /// </param>
        /// <exception cref="MqttClientException">MqttClientException</exception>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180876">MQTT Subscribe</a>
        /// for more details about the protocol subscription
        /// </remarks>
        ValueTask<Task<SubscribeReturnCode[]?>> SubscribeAsync( IMqttLogger m, params Subscription[] subscriptions );

        /// <summary>
        /// Represents the protocol publish, which consists of sending a PUBLISH packet
        /// and awaiting the corresponding ACK packet, if applies, based on the QoS defined
        /// </summary>
        /// <param name="message">
        /// The application message to publish to the Server.
        /// See <see cref="OutgoingApplicationMessage" /> for more details about the application messages
        /// </param>
        /// <param name="qos">
        /// The Quality Of Service (QoS) associated to the application message, which determines 
        /// the sequence of acknowledgements that Client and Server should send each other to consider the message as delivered
        /// See <see cref="QualityOfService" /> for more details about the QoS values
        /// </param>
        /// <param name="retain">
        /// Indicates if the application message should be retained by the Server for future subscribers.
        /// Only the last message of each topic is retained
        /// </param>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180850">MQTT Publish</a>
        /// for more details about the protocol publish
        /// </remarks>
        ValueTask<Task> PublishAsync( IMqttLogger m, OutgoingApplicationMessage message );

        /// <summary>
        /// Represents the protocol unsubscription, which consists of sending an UNSUBSCRIBE packet
        /// and awaiting the corresponding UNSUBACK packet from the Server
        /// </summary>
        /// <param name="topics">
        /// The list of topics to unsubscribe from. Once the unsubscription completes, no more application messages for those topics
        /// will arrive to <see cref="MessageReceived"/> 
        /// </param>
        /// <exception cref="MqttClientException">MqttClientException</exception>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180885">MQTT Unsubscribe</a>
        /// for more details about the protocol unsubscription
        /// </remarks>
        ValueTask<Task> UnsubscribeAsync( IMqttLogger m, params string[] topics );

        /// <summary>
        /// Represents the protocol disconnection, which consists of sending a DISCONNECT packet to the Server
        /// No acknowledgement is sent by the Server on the disconnection
        /// Once the client is successfully disconnected, the <see cref="Disconnected"/> event will be fired 
        /// </summary>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180903">MQTT Disconnect</a>
        /// for more details about the protocol disconnection
        /// </remarks>
        ValueTask DisconnectAsync( IMqttLogger m );
    }

}
