using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.P2P.Tests
{
    public class TopicFilterMatch
    {
        [TestCase("foo/", "foo", false)]
        [TestCase("foo", "foo/", false)]
        [TestCase("/foo", "foo", false)]
        [TestCase("foo", "/foo", false)]
        [TestCase( "foo", "foo", true)]
        [TestCase( "foo/", "foo/#", true)]
        public void trailing_slash_is_different_than_none( string topic, string filter, bool result)
        {
            MqttTopicFilterComparer.IsMatch(topic, filter).Should().Be(result);
        }
    }
}
