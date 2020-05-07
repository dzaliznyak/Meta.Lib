using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public partial class MetaPubSub : IMetaPubSub
    {
        /// <summary>
        /// Subscribe to a message of TMessage type
        /// </summary>
        /// <typeparam name="TMessage">Type of message to subscribe to</typeparam>
        /// <param name="handler">Client defined function that will be called when the message has arrived</param>
        /// <param name="match">The delegate that defines the conditions of the message to subscribe for. If null all messages of the specified type will be received</param>
        public void Subscribe<TMessage>(Func<TMessage, Task> handler, Predicate<TMessage> match = null)
            where TMessage : class, IPubSubMessage
        {
            _messageHub.Subscribe(handler, match);
        }

        /// <summary>
        /// Unsubscribe from a message of TMessage type
        /// </summary>
        /// <typeparam name="TMessage">Type of message to unsubscribe from</typeparam>
        /// <param name="handler">The same callback function that was passed to the Subscribe method</param>
        public void Unsubscribe<TMessage>(Func<TMessage, Task> handler)
            where TMessage : class, IPubSubMessage
        {
            _messageHub.Unsubscribe(handler);
        }

        /// <summary>
        /// Publish a message
        /// </summary>
        /// <param name="message">An instance of any class derived from IPubSubMessage</param>
        /// <returns>A Task that can be awaited until the message has been processed by all subscribers</returns>
        public Task Publish(IPubSubMessage message)
        {
            return _messageHub.Publish(message);
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
        public Task<TMessage> When<TMessage>(int millisecondsTimeout, CancellationToken cancellationToken = default)
            where TMessage : class, IPubSubMessage
        {
            return _requestResponseProcessor.When<TMessage>(millisecondsTimeout, cancellationToken);
        }

        /// <summary>
        /// Publishes the message and waits until the response message will arrive
        /// </summary>
        /// <typeparam name="TResponse">Type of the response message to wait for</typeparam>
        /// <param name="message">An instance of any class derived from IPubSubMessage</param>
        /// <param name="millisecondsTimeout">Time interval during which the response message must be received otherwise the TimeoutException will be thrown</param>
        /// <param name="cancellationToken">The cancellation token that can be used to discard awaiting the response message</param>
        /// <returns>A Task that can be awaited until the response message has been arrived</returns>
        public Task<TResponse> Process<TResponse>(IPubSubMessage message, int millisecondsTimeout, CancellationToken cancellationToken = default)
            where TResponse : class, IPubSubMessage
        {
            return _requestResponseProcessor.Process<TResponse>(message, millisecondsTimeout, cancellationToken);
        }

        /// <summary>
        /// Saves the message that will be published after a time delay
        /// </summary>
        /// <param name="message">An instance of any class derived from IPubSubMessage</param>
        /// <param name="millisecondsDelay">The time delay before publishing the message</param>
        /// <param name="cancellationToken">The cancellation token that can be used to discard publishing the message</param>
        public void Schedule(IPubSubMessage message, int millisecondsDelay, CancellationToken cancellationToken = default)
        {
            _messageScheduler.Schedule(message, millisecondsDelay, cancellationToken);
        }
    }
}
