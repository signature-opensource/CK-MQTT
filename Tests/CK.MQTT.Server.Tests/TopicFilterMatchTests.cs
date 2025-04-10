using Shouldly;
using NUnit.Framework;

namespace CK.MQTT.Server.Tests;

public class TopicFilterMatchTests
{
    [TestCase( "foo/", "foo", false )]
    [TestCase( "foo", "foo/", false )]
    [TestCase( "/foo", "foo", false )]
    [TestCase( "foo", "/foo", false )]
    [TestCase( "foo", "foo", true )]
    [TestCase( "foo/", "foo/#", true )]
    public void trailing_slash_is_different_than_none( string topic, string filter, bool result )
    {
        MQTTTopicFilterComparer.IsMatch( topic, filter ).ShouldBe( result );
    }
}
