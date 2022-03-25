using System.Threading.Tasks;

namespace CK.MQTT.Common.Pumps
{
    public interface IState
    {
        public Task CloseAsync();
    }
}
