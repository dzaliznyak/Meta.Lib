using System;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class Subscription<TMessage> : ISubscription
    {
        readonly Func<TMessage, Task> _handler;
        readonly Predicate<TMessage> _filter;

        public Subscription(Func<TMessage, Task> handler, Predicate<TMessage> filter)
        {
            _handler = handler;
            _filter = filter;
        }

        public bool IsSameHandler(object handler)
        {
            return _handler.Equals(handler);
        }

        public Task Deliver(object message)
        {
            return _handler((TMessage)message);
        }

        public bool ShouldDeliver(object message)
        {
            if (_filter == null)
                return true;
            return _filter((TMessage)message);
        }
    }
}
