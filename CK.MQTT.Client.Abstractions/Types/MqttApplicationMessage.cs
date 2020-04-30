using System;

namespace CK.MQTT
{
	/// <summary>
	/// Represents an application message, which correspond to the unit of information
	/// sent from Client to Server and from Server to Client
	/// </summary>
	public class ApplicationMessage
	{
		readonly ReadOnlyMemory<byte> _payload;

		/// <summary>
		/// Initializes a new instance of the <see cref="ApplicationMessage" /> class,
		/// specifying the topic and payload of the message
		/// </summary>
		/// <param name="topic">
		/// Topic associated with the message
		/// Any subscriber of this topic should receive the corresponding messages
		/// </param>
		/// <param name="payload">Content of the message, as a byte array</param>
		public ApplicationMessage(string topic, ReadOnlyMemory<byte> payload)
		{
			Topic = topic;
			_payload = payload;
		}

		/// <summary>
		/// Topic associated with the message.
		/// Any subscriber of this topic should receive the corresponding messages.
		/// </summary>
		public string Topic { get; }

		/// <summary>
		/// Content of the message. This must be accessed only during the handling of the message.
		/// </summary>
		public ReadOnlySpan<byte> Payload => _payload.Span;
	}
}
