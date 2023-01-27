using System;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class Subscription<TMessage> : ISubscription
            where TMessage : class, IPubSubMessage
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

        public Task Deliver(IPubSubMessage message)
        {
            return _handler(message as TMessage);
        }

        public bool ShouldDeliver(IPubSubMessage message)
        {
            if (_filter == null)
                return true;
            return _filter(message as TMessage);
        }
    }
}
