using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public interface ISubscription
    {
        bool IsSameHandler(object handler);
        Task Deliver(IPubSubMessage message);
        bool ShouldDeliver(IPubSubMessage message);
    }
}
