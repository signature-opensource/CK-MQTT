using CK.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Channels
{
    /// <summary>
    /// Represents a mechanism to send and receive information between two endpoints
    /// </summary>
    /// <typeparam name="T">The type of information that will go through the channel</typeparam>
    public interface IMqttChannel<T> : IDisposable
        where T : class
    {
        ValueTask CloseAsync( IActivityMonitor m, CancellationToken cancellationToken );

        /// <summary>
        /// Indicates if the channel is connected to the underlying stream or not
        /// </summary>
		bool IsConnected { get; }

        /// <summary>
        /// Emit informtion received by the channel.
        /// </summary>
        event SequentialEventHandler<IMqttChannel<T>, T> Received;

        /// <summary>
        /// Emit information sent by the channel.
        /// </summary>
		event SequentialEventHandler<IMqttChannel<T>, T> Sent;

        Task<TReceive?> WaitMessageReceivedAsync<TReceive>( Func<TReceive, bool>? predicate = null, int timeoutMillisecond = -1 )
            where TReceive : class, T;

        /// <summary>
        /// Sends information to the other end, through the underlying stream
        /// </summary>
        /// <param name="message">
        /// Message to send to the other end of the channel
        /// </param>
		/// <exception cref="MqttException">MqttException</exception>
		ValueTask SendAsync( IActivityMonitor m, T message, CancellationToken cancellationToken );
    }
}
