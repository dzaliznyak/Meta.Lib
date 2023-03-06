using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public interface IMetaPubSub
    {
        /// <summary>
        /// Subscribes to a message of TMessage type. Use Unsubscribe<TMessage>() to unregister message handler. Subsequent call to the Subscribe with the same handler does nothing
        /// </summary>
        /// <typeparam name="TMessage">Type of a message to subscribe to</typeparam>
        /// <param name="handler">Client defined function that will be called when the message has arrived</param>
        /// <param name="match">The delegate that defines the conditions of the message to subscribe for. If null all messages of the specified type will be received</param>
        /// <exception cref="ArgumentNullException" />
        void Subscribe<TMessage>(Func<TMessage, Task> handler, Predicate<TMessage> match = null);

        /// <summary>
        /// Subscribes to a message. Use Unsubscribe() to unregister message handler. Subsequent call to the Subscribe with the same handler does nothing
        /// </summary>
        /// <param name="type">Type of a message to subscribe to</param>
        /// <param name="handler">Client defined function that will be called when the message has arrived</param>
        /// <param name="match">The delegate that defines the conditions of the message to subscribe for. If null all messages of the specified type will be received</param>
        /// <exception cref="ArgumentNullException"></exception>
        void Subscribe(Type type, Func<object, Task> handler, Predicate<object> match = null);

        /// <summary>
        /// Unsubscribes from a message of TMessage type
        /// </summary>
        /// <typeparam name="TMessage">Type of message to unsubscribe from</typeparam>
        /// <param name="handler">The same callback function that was passed to the Subscribe method</param>
        void Unsubscribe<TMessage>(Func<TMessage, Task> handler);

        /// <summary>
        /// Unsubscribes from a message
        /// </summary>
        /// <param name="type">Type of message to unsubscribe from</param>
        /// <param name="handler">The same callback function that was passed to the Subscribe method</param>
        void Unsubscribe(Type type, Func<object, Task> handler);

        /// <summary>
        /// Publishes a message
        /// </summary>
        /// <param name="message">An instance of any class</param>
        /// <param name="options">This refers to an optional parameter that contains the parameters used for sending the message.</param>
        /// <returns>A Task that can be awaited until the message has been processed by all subscribers</returns>
        /// <exception cref="AggregateException" />
        /// <exception cref="TimeoutException" />
        /// <exception cref="NoSubscribersException" />
        Task Publish<TMessage>(TMessage message, PubSubOptions options = null);

        /// <summary>
        /// Awaits when a message of the TMessage type will arrive
        /// </summary>
        /// <typeparam name="TMessage">Type of the message to wait for</typeparam>
        /// <param name="millisecondsTimeout">Time interval during which a message must be received otherwise the TimeoutException will be thrown</param>
        /// <param name="match">This refers to the delegate that specifies the criteria for the message to be waited for. If the delegate is null, any message of the specified type will be sufficient.</param>
        /// <param name="cancellationToken">The cancellation token that can be used to discard awaiting the message</param>
        /// <returns>A Task that can be awaited until the message has been arrived</returns>
        /// <exception cref="TimeoutException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="ArgumentException"></exception>
        Task<TMessage> When<TMessage>(int millisecondsTimeout,
            Predicate<TMessage> match = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes the message and waits until the response message will arrive
        /// </summary>
        /// <typeparam name="TMessage">Type of the message to send</typeparam>
        /// <typeparam name="TResponse">Type of the response message to wait for</typeparam>
        /// <param name="message">The message to send. Can be an instance of any class</param>
        /// <param name="responseTimeoutMs">Time interval during which the response message must be received otherwise the TimeoutException will be thrown</param>
        /// <param name="options">This refers to an optional parameter that contains the parameters used for sending the message.</param>
        /// <param name="match">This refers to the delegate that specifies the criteria for the message to be waited for. If the delegate is null, any message of the specified type will be sufficient.</param>
        /// <param name="cancellationToken">The cancellation token that can be used to discard awaiting the response message</param>
        /// <returns>A Task that can be awaited until the response message has been arrived</returns>
        /// <exception cref="TimeoutException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="ArgumentException"></exception>
        Task<TResponse> Process<TMessage, TResponse>(TMessage message,
            int responseTimeoutMs = 5000,
            PubSubOptions options = null,
            Predicate<TResponse> match = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes the message and waits until the response message will arrive
        /// </summary>
        /// <param name="responseType">Type of the response message to wait for</param>
        /// <param name="message">The message to send. Can be an instance of any class</param>
        /// <param name="responseTimeoutMs">Time interval during which the response message must be received otherwise the TimeoutException will be thrown</param>
        /// <param name="options">This refers to an optional parameter that contains the parameters used for sending the message.</param>
        /// <param name="cancellationToken">The cancellation token that can be used to discard awaiting the response message</param>
        /// <returns>A Task that can be awaited until the response message has been arrived</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="TimeoutException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        Task<object> Process(Type messageType,
            object message,
            int responseTimeoutMs = 5000,
            PubSubOptions options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the message that will be published after a time delay
        /// </summary>
        /// <param name="message">An instance of any class</param>
        /// <param name="millisecondsDelay">The time delay before publishing the message</param>
        /// <param name="options">This refers to an optional parameter that contains the parameters used for sending the message.</param>
        /// <param name="cancellationToken">The cancellation token that can be used to cancel the publication of the message</param>
        /// <exception cref="ArgumentException"></exception>
        void Schedule<TMessage>(TMessage message,
            int millisecondsDelay,
            PubSubOptions options = null,
            CancellationToken cancellationToken = default);
    }
}
