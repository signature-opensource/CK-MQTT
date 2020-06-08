using CK.Core;
using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Represents an MQTT Client
    /// </summary>
    public interface IMqttClient
    {
        /// <summary>
        /// Event raised when the Client gets disconnected in asynchronous way, each async handler being called in parallel
        /// with the other ones.
        /// The Client disconnection could be caused by a protocol disconnect, an error or a remote disconnection
        /// produced by the Server.
        /// See <see cref="MqttEndpointDisconnected"/> for more details on the disconnection information
        /// </summary>
        event ParallelEventHandlerAsync<IMqttClient, MqttEndpointDisconnected> ParallelDisconnectedAsync;

        /// <summary>
        /// Event raised when the Client gets disconnected, synchronously.
        /// The Client disconnection could be caused by a protocol disconnect, an error or a remote disconnection
        /// produced by the Server.
        /// See <see cref="MqttEndpointDisconnected"/> for more details on the disconnection information
        /// </summary>
        event SequentialEventHandler<IMqttClient, MqttEndpointDisconnected> Disconnected;

        /// <summary>
        /// Event raised when the Client gets disconnected in asynchronous way, each async handler being called
        /// one after the other.
        /// The Client disconnection could be caused by a protocol disconnect, an error or a remote disconnection
        /// produced by the Server.
        /// See <see cref="MqttEndpointDisconnected"/> for more details on the disconnection information
        /// </summary>
        event SequentialEventHandlerAsync<IMqttClient, MqttEndpointDisconnected> DisconnectedAsync;

        /// <summary>
        /// Id of the connected Client. <see cref="null"/> until connected.
        /// This Id correspond to the <see cref="MqttClientCredentials.ClientId"/> parameter passed to 
        /// <see cref="ConnectAsync(IActivityMonitor, MqttClientCredentials, LastWill?, bool)"/> method or
        /// has been provided by the server.
        /// </summary>
        string? ClientId { get; }

        /// <summary>
        /// Checks the connection: the Client must be connected, ie. a CONNECT packet has been sent by
        /// calling <see cref="ConnectAsync( IActivityMonitor, MqttClientCredentials, LastWill, bool)"/> and
        /// the connection succeed.
        /// </summary>
        /// <param name="m">The monitor that will be used.</param>
        ValueTask<bool> CheckConnectionAsync( IActivityMonitor m );

        /// <summary>
        /// Event raised for each received message in asynchronous way, each async handler being called in parallel
        /// with the other ones.
        /// </summary>
        event ParallelEventHandlerAsync<IMqttClient, OutgoingApplicationMessage> ParallelMessageReceivedAsync;

        /// <summary>
        /// Event raised for each received message, synchronously.
        /// </summary>
        event SequentialEventHandler<IMqttClient, OutgoingApplicationMessage> MessageReceived;

        /// <summary>
        /// Asynchronously waits for the next <see cref="MessageReceived"/> that matches an optional <paramref name="predicate"/>
        /// during an optional <paramref name="timeoutMillisecond"/> time span.
        /// </summary>
        /// <param name="predicate">The predicate that received message must satisfy.</param>
        /// <param name="timeoutMillisecond">The timeout in milliseconds.</param>
        /// <returns>The message or null if the timeout expired before the message has been received.</returns>
        Task<OutgoingApplicationMessage?> WaitMessageReceivedAsync( Func<OutgoingApplicationMessage, bool>? predicate = null, int timeoutMillisecond = -1 );

        /// <summary>
        /// Event raised for each received message in asynchronous way, each async handler being called
        /// one after the other.
        /// </summary>
        event SequentialEventHandlerAsync<IMqttClient, OutgoingApplicationMessage> MessageReceivedAsync;

        /// <summary>
        /// Represents the protocol connection, which consists of sending a CONNECT packet
        /// and awaiting the corresponding CONNACK packet from the Server
        /// </summary>
        /// <param name="credentials">
        /// The credentials used to connect to the Server. See <see cref="MqttClientCredentials" /> for more details on the credentials information
        /// </param>
        /// <param name="will">
        /// The last will message to send from the Server when an unexpected Client disconnection occurrs. 
        /// See <see cref="LastWill" /> for more details about the will message structure
        /// </param>
        /// <param name="cleanSession">
        /// Indicates if the session state between Client and Server must be cleared between connections
        /// Defaults to false, meaning that session state will be preserved by default accross connections
        /// </param>
        /// <returns>
        /// Returns the state of the client session created as part of the connection
        /// See <see cref="SessionState" /> for more details about the session state values
        /// </returns>
        /// <exception cref="MqttClientException">MqttClientException</exception>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180841">MQTT Connect</a>
        /// for more details about the protocol connection
        /// </remarks>
        Task<ConnectResult> ConnectAsync( IActivityMonitor m, MqttClientCredentials credentials, LastWill? will = null, bool cleanSession = false );

        Task<ConnectResult> ConnectAnonymousAsync( IActivityMonitor m, LastWill? will = null );

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
        Task<Task<IReadOnlyCollection<SubscribeReturnCode>?>> SubscribeAsync( IActivityMonitor m, params Subscription[] subscriptions );

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
        ValueTask<ValueTask> PublishAsync( IActivityMonitor m, string topic, ReadOnlyMemory<byte> payload, QualityOfService qos, bool retain = false );

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
        Task<Task<bool>> UnsubscribeAsync( IActivityMonitor m, IEnumerable<string> topics );

        /// <summary>
        /// Represents the protocol disconnection, which consists of sending a DISCONNECT packet to the Server
        /// No acknowledgement is sent by the Server on the disconnection
        /// Once the client is successfully disconnected, the <see cref="Disconnected"/> event will be fired 
        /// </summary>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180903">MQTT Disconnect</a>
        /// for more details about the protocol disconnection
        /// </remarks>
        Task DisconnectAsync( IActivityMonitor m );
    }

}
