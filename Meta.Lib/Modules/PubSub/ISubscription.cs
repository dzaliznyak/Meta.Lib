using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public interface ISubscription
    {
        bool IsSameHandler(object handler);
        Task Deliver(object message);
        bool ShouldDeliver(object message);
    }
}
