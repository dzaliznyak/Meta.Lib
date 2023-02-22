using Meta.Lib.Exceptions;
using Meta.Lib.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class MessageHub
    {
        readonly ILogger _logger;
        readonly Dictionary<Type, Node> _nodes = new Dictionary<Type, Node>();
        readonly DelayedMessages _delayedMessages;

        internal MessageHub(ILogger logger, DelayedMessages delayedMessages)
        {
            _logger = logger;
            _delayedMessages = delayedMessages;
        }

        internal void Subscribe<TMessage>(Func<TMessage, Task> action, Predicate<TMessage> predicate)
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
                _delayedMessages.OnNewSubscriber(typeof(TMessage), subscription);
            }
        }

        internal void Unsubscribe<TMessage>(Func<TMessage, Task> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (_nodes.TryGetValue(typeof(TMessage), out Node node))
            {
                node.TryRemove(action);
            }
        }

        internal Task Publish<TMessage>(TMessage message, PubSubOptions options)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (_nodes.TryGetValue(message.GetType(), out Node node))
                return Deliver(node.Subscribers, message, options);
            else
                return Deliver(ImmutableArray<ISubscription>.Empty, message, options);
        }

        async Task Deliver(IReadOnlyCollection<ISubscription> subscribers, object message, PubSubOptions options)
        {
            bool hasAtLeastOneSubscriber = false;
            List<Exception> exceptions = null;

            // deliver to subscribers
            foreach (var item in subscribers)
            {
                try
                {
                    if (item.ShouldDeliver(message))
                    {
                        hasAtLeastOneSubscriber = true;
                        await item.Deliver(message);
                    }
                }
                catch (Exception ex)
                {
                    if (exceptions == null)
                        exceptions = new List<Exception>();
                    exceptions.Add(ex);
                }
            }

            if (exceptions != null)
                throw new AggregateException(exceptions).Fix();

            // if no one processed the message, store it and wait for subscriber to come
            if (options.DeliverAtLeastOnce && !hasAtLeastOneSubscriber)
            {
                if (options.WaitForSubscriberTimeout > 0)
                {
                    _logger?.LogDebug("Delayed <<<{Message}>>> for {Timeout} ms", message.GetType().Name, options.WaitForSubscriberTimeout);
                    await _delayedMessages.Enqueue(message, options);
                }
                else
                {
                    throw new NoSubscribersException("Failed to deliver the message - no one is listening");
                }
            }
        }


    }
}
