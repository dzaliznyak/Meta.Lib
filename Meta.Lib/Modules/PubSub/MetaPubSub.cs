using Meta.Lib.Exceptions;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public partial class MetaPubSub : IMetaPubSub
    {
        readonly ILogger _logger;
        readonly DelayedMessages _delayedMessages;
        readonly MessageHub _messageHub;
        readonly RequestResponseProcessor _requestResponseProcessor;

        public MetaPubSub(ILogger logger = null)
        {
            _logger = logger;

            _delayedMessages = new DelayedMessages();
            _messageHub = new MessageHub(logger, _delayedMessages);
            _requestResponseProcessor = new RequestResponseProcessor(this);
        }

        /// <summary>
        /// Subscribe to a message of TMessage type
        /// </summary>
        /// <typeparam name="TMessage">Type of a message to subscribe to</typeparam>
        /// <param name="handler">Client defined function that will be called when the message has arrived</param>
        /// <param name="match">The delegate that defines the conditions of the message to subscribe for. If null all messages of the specified type will be received</param>
        /// <exception cref="ArgumentNullException" />
        public void Subscribe<TMessage>(Func<TMessage, Task> handler, Predicate<TMessage> match = null)
        {
            _messageHub.Subscribe(handler, match);
        }

        public void Subscribe(Type type, Func<object, Task> handler, Predicate<object> match = null)
        {
            _messageHub.Subscribe(type, handler, match);
        }

        /// <summary>
        /// Unsubscribe from a message of TMessage type
        /// </summary>
        /// <typeparam name="TMessage">Type of message to unsubscribe from</typeparam>
        /// <param name="handler">The same callback function that was passed to the Subscribe method</param>
        public void Unsubscribe<TMessage>(Func<TMessage, Task> handler)
        {
            _messageHub.Unsubscribe(handler);
        }

        public void Unsubscribe(Type type, Func<object, Task> handler)
        {
            _messageHub.Unsubscribe(type, handler);
        }

        /// <summary>
        /// Publish a message
        /// </summary>
        /// <param name="message">An instance of any class derived from IPubSubMessage</param>
        /// <returns>A Task that can be awaited until the message has been processed by all subscribers</returns>
        /// <exception cref="AggregateException" />
        /// <exception cref="TimeoutException" />
        /// <exception cref="NoSubscribersException" />
        public Task Publish<TMessage>(TMessage message, PubSubOptions options = null)
        {
            return _messageHub.Publish(message, options);
        }

        /// <summary>
        /// Awaits when a message of the TMessage type will arrive
        /// </summary>
        /// <typeparam name="TMessage">Type of the message to wait for</typeparam>
        /// <param name="millisecondsTimeout">Time interval during which a message must be received otherwise the TimeoutException will be thrown</param>
        /// <param name="cancellationToken">The cancellation token that can be used to discard awaiting the message</param>
        /// <returns>A Task that can be awaited until the message has been arrived</returns>
        /// <exception cref="TimeoutException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public Task<TMessage> When<TMessage>(int millisecondsTimeout, Predicate<TMessage> match = null,
            CancellationToken cancellationToken = default)
        {
            if (millisecondsTimeout <= 0)
                throw new ArgumentException("millisecondsTimeout must be greater than zero");

            return _requestResponseProcessor.When(millisecondsTimeout, match, cancellationToken);
        }

        /// <summary>
        /// Publishes the message and waits until the response message will arrive
        /// </summary>
        /// <typeparam name="TResponse">Type of the response message to wait for</typeparam>
        /// <param name="message">An instance of any class derived from IPubSubMessage</param>
        /// <param name="responseTimeoutMs">Time interval during which the response message must be received otherwise the TimeoutException will be thrown</param>
        /// <param name="cancellationToken">The cancellation token that can be used to discard awaiting the response message</param>
        /// <returns>A Task that can be awaited until the response message has been arrived</returns>
        /// <exception cref="TimeoutException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public Task<TResponse> Process<TMessage, TResponse>(TMessage message, int responseTimeoutMs = 5000, PubSubOptions options = null,
            Predicate<TResponse> match = null, CancellationToken cancellationToken = default)
        {
            if (responseTimeoutMs <= 0)
                throw new ArgumentException("Message response timeout must be greater than zero");

            return _requestResponseProcessor.Process(message, responseTimeoutMs, options, match, cancellationToken);
        }

        public Task<object> Process(Type responseType, object message, int responseTimeoutMs = 5000, PubSubOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (responseTimeoutMs <= 0)
                throw new ArgumentException("Message response timeout must be greater than zero");

            return _requestResponseProcessor.Process(responseType, message, responseTimeoutMs, options, cancellationToken);
        }

        /// <summary>
        /// Saves the message that will be published after a time delay
        /// </summary>
        /// <param name="message">An instance of any class derived from IPubSubMessage</param>
        /// <param name="millisecondsDelay">The time delay before publishing the message</param>
        /// <param name="cancellationToken">The cancellation token that can be used to discard publishing the message</param>
        /// <exception cref="ArgumentException"></exception>
        public void Schedule<TMessage>(TMessage message, int millisecondsDelay, PubSubOptions options = null, CancellationToken cancellationToken = default)
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
