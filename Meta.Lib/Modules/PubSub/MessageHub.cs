using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal interface ISubscription
    {
        bool ActionEquals(object action);
        Task Deliver(IPubSubMessage message);
        bool ShouldDeliver(IPubSubMessage message);
    }

    internal class Subscriber
    {
        public ISubscription Subscription { get; }

        public Subscriber(ISubscription subscription)
        {
            Subscription = subscription;
        }
    }

    internal class Subscription<TMessage> : ISubscription
            where TMessage : class, IPubSubMessage
    {
        readonly Func<TMessage, Task> _action;
        readonly Predicate<TMessage> _predicate;

        public Subscription(Func<TMessage, Task> action, Predicate<TMessage> predicate)
        {
            _action = action;
            _predicate = predicate;
        }

        public bool ActionEquals(object action)
        {
            return _action.Equals(action);
        }

        public Task Deliver(IPubSubMessage message)
        {
            return _action(message as TMessage);
        }

        public bool ShouldDeliver(IPubSubMessage message)
        {
            if (_predicate == null)
                return true;
            return _predicate(message as TMessage);
        }
    }

    internal class Node
    {
        readonly object _lock = new object();
        ImmutableList<Subscriber> _subscribers = ImmutableList<Subscriber>.Empty;

        public IReadOnlyCollection<Subscriber> Subscribers => _subscribers;

        public void Add(Subscriber subscriber)
        {
            lock (_lock)
            {
                _subscribers = _subscribers.Add(subscriber);
            }
        }

        public void Remove(Subscriber subscriber)
        {
            lock (_lock)
            {
                _subscribers = _subscribers.Remove(subscriber);
            }
        }

        public Subscriber Find(Predicate<Subscriber> match)
        {
            return _subscribers.Find(match);
        }
    }

    internal class MessageHub
    {
        readonly Dictionary<Type, Node> _nodes =
            new Dictionary<Type, Node>();

        readonly Func<IReadOnlyCollection<Subscriber>, IPubSubMessage, Task> _onPublished;
        readonly Action<Type, Subscriber> _onNewSubscriber;

        public MessageHub(Func<IReadOnlyCollection<Subscriber>, IPubSubMessage, Task> onPublished,
                          Action<Type, Subscriber> onNewSubscriber)
        {
            _onPublished = onPublished;
            _onNewSubscriber = onNewSubscriber;
        }

        public void Subscribe<TMessage>(Func<TMessage, Task> action, Predicate<TMessage> predicate)
            where TMessage : class, IPubSubMessage
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (!_nodes.TryGetValue(typeof(TMessage), out Node node))
            {
                lock (_nodes)
                {
                    if (!_nodes.TryGetValue(typeof(TMessage), out node))
                    {
                        node = new Node();
                        _nodes.Add(typeof(TMessage), node);
                    }
                }
            }

            if (node.Find(x => x.Subscription.ActionEquals(action)) == null)
            {
                var subscriber = new Subscriber(new Subscription<TMessage>(action, predicate));
                node.Add(subscriber);
                _onNewSubscriber(typeof(TMessage), subscriber);
            }
        }

        public void Unsubscribe<TMessage>(Func<TMessage, Task> action)
            where TMessage : class, IPubSubMessage
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (_nodes.TryGetValue(typeof(TMessage), out Node node))
            {
                var subscriber = node.Find(x => x.Subscription.ActionEquals(action));

                if (subscriber != null)
                    node.Remove(subscriber);
            }
        }

        public Task Publish(IPubSubMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (_nodes.TryGetValue(message.GetType(), out Node node))
                return _onPublished(node.Subscribers, message);
            else
                return _onPublished(ImmutableList<Subscriber>.Empty, message);
        }

    }
}
