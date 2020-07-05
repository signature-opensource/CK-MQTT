using FluentAssertions;
using NUnit.Framework;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;
namespace CK.MQTT.Client
{
    public class EventHandlersExtensionTests
    {
        class Unit
        {
        }

        [Test]
        public async Task simple_await_work()
        {
            SequentialEventHandlerSender<object, Unit> eventEmitter = new SequentialEventHandlerSender<object, Unit>();
            var task = eventEmitter.WaitAsync();
            task.IsCompleted.Should().BeFalse();
            task.IsFaulted.Should().BeFalse();
            eventEmitter.Raise( TestHelper.Monitor, this, new Unit() );
            await Task.Yield();
            await Task.Delay( 500 );
            task.IsCompleted.Should().BeTrue();
            task.IsFaulted.Should().BeTrue();
        }
    }
}
