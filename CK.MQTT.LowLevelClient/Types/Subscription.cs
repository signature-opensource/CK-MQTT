namespace CK.MQTT;


/// <summary>
/// Represent a subscription to a topic.
/// <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc384800444_Toc384800436">See documentation for more information.</a>
/// </summary>
public class Subscription
{
    /// <summary>
    /// Instantiate a new <see cref="Subscription"/>.
    /// </summary>
    /// <param name="topicFilter">The topic filter.</param>
    /// <param name="requestedQos">The requested QoS.</param>
    public Subscription( string topicFilter, QualityOfService requestedQos )
    {
        TopicFilter = topicFilter;
        MaximumQualityOfService = requestedQos;
    }

    /// <summary>
    /// <a href="https://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc384800444">The topic filter.</a>
    /// </summary>
    public string TopicFilter { get; set; }

    /// <summary>
    /// The maximum QoS the broker should send the packet matching this topic filter.
    /// <a href="https://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc384800440">See the specification for more details.</a>
    /// </summary>
    public QualityOfService MaximumQualityOfService { get; set; }
}
