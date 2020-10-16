using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests
{
    class TestChannelFactory : IMqttChannelFactory
    {
        readonly TestChannel _testChannel;

        public TestChannelFactory(TestChannel testChannel)
        {
            _testChannel = testChannel;
        }
        public ValueTask<IMqttChannel> CreateAsync( IActivityMonitor m, string connectionString )
            => new ValueTask<IMqttChannel>( _testChannel );
    }
}
