using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    /// <inheritdoc />
    public class MetaPubSub : IMetaPubSub
    {
        readonly DelayedMessages _delayedMessages;
        readonly MessageHub _messageHub;
        readonly RequestResponseProcessor _requestResponseProcessor;

        public MetaPubSub(ILogger logger = null)
        {
            _delayedMessages = new DelayedMessages();
            _messageHub = new MessageHub(logger, _delayedMessages);
            _requestResponseProcessor = new RequestResponseProcessor(this);
        }

        public void Subscribe<TMessage>(Func<TMessage, Task> handler, Predicate<TMessage> match = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _messageHub.Subscribe(handler, match);
        }

        public void Subscribe(Type type, Func<object, Task> handler, Predicate<object> match = null)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _messageHub.Subscribe(type, handler, match);
        }

        public void Unsubscribe<TMessage>(Func<TMessage, Task> handler)
        {
            _messageHub.Unsubscribe(handler);
        }

        public void Unsubscribe(Type type, Func<object, Task> handler)
        {
            _messageHub.Unsubscribe(type, handler);
        }

        public Task Publish<TMessage>(TMessage message, PubSubOptions options = null)
        {
            return _messageHub.Publish(message, options);
        }

        public Task<TMessage> When<TMessage>(
            int millisecondsTimeout,
            Predicate<TMessage> match = null,
            CancellationToken cancellationToken = default)
        {
            if (millisecondsTimeout <= 0)
                throw new ArgumentException("millisecondsTimeout must be greater than zero");

            return _requestResponseProcessor.When(millisecondsTimeout, match, cancellationToken);
        }

        public Task<TResponse> Process<TMessage, TResponse>(
            TMessage message,
            int responseTimeoutMs = 5000,
            PubSubOptions options = null,
            Predicate<TResponse> match = null,
            CancellationToken cancellationToken = default)
        {
            if (responseTimeoutMs <= 0)
                throw new ArgumentException("Message response timeout must be greater than zero");

            return _requestResponseProcessor.Process(message, responseTimeoutMs, options, match, cancellationToken);
        }

        public Task<object> Process(
            Type responseType,
            object message,
            int responseTimeoutMs = 5000,
            PubSubOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (responseTimeoutMs <= 0)
                throw new ArgumentException("Message response timeout must be greater than zero");

            return _requestResponseProcessor.Process(responseType, message, responseTimeoutMs, options, cancellationToken);
        }

        public void Schedule<TMessage>(
            TMessage message,
            int millisecondsDelay,
            PubSubOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (millisecondsDelay <= 0)
                throw new ArgumentException("millisecondsDelay must be greater than zero");

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(millisecondsDelay, cancellationToken);
                    await Publish(message, options);
                }
                catch (Exception)
                {
                }
            });
        }


    }
}
