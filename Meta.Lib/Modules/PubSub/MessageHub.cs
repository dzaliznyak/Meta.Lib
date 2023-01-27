using Meta.Lib.Modules.Logger;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class MessageHub
    {
        readonly Dictionary<Type, Node> _nodes = new Dictionary<Type, Node>();
        readonly IMetaLogger _logger;
        readonly Func<IReadOnlyCollection<ISubscription>, IPubSubMessage, Task> _onPublished;
        readonly Action<Type, ISubscription> _onNewSubscriber;

        public MessageHub(IMetaLogger logger,
                          Func<IReadOnlyCollection<ISubscription>, IPubSubMessage, Task> onPublished,
                          Action<Type, ISubscription> onNewSubscriber)
        {
            _logger = logger;
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
                // create node for this TMessage, if not exists
                lock (_nodes)
                {
                    if (!_nodes.TryGetValue(typeof(TMessage), out node))
                    {
                        node = new Node();
                        _nodes.Add(typeof(TMessage), node);
                    }
                }
            }

            if (node.TryAdd(action, predicate, out ISubscription subscription))
            {
                _onNewSubscriber(typeof(TMessage), subscription);
            }
        }

        public void Unsubscribe<TMessage>(Func<TMessage, Task> action)
            where TMessage : class, IPubSubMessage
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (_nodes.TryGetValue(typeof(TMessage), out Node node))
            {
                node.TryRemove(action);
            }
        }

        public Task Publish(IPubSubMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (_nodes.TryGetValue(message.GetType(), out Node node))
                return _onPublished(node.Subscribers, message);
            else
                return _onPublished(ImmutableArray<ISubscription>.Empty, message);
        }

    }
}
