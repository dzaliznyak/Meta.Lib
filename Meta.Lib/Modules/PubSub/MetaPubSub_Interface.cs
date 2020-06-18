using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public partial class MetaPubSub : IMetaPubSub
    {
        public Task ConnectServer(string pipeName)
        {
            if (_proxy != null)
                throw new InvalidOperationException("Already connected");

            _proxy = new RemotePubSubProxy(this, pipeName);

            _proxy.Disconnected += Proxy_Disconnected;

            return _proxy.Connect();
        }

        async void Proxy_Disconnected(object sender, EventArgs e)
        {
            bool connected = false;
            while (!connected)
            {
                try
                {
                    await _proxy.Connect();
                    connected = true;
                }
                catch (Exception)
                {
                }
            }
        }

        public void StartServer(string pipeName)
        {
            if (_proxy != null)
                throw new InvalidOperationException("Cannot be started as a server when has been connected to another server");

            _pipeConections.Start(pipeName);
        }

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

        public Task SubscribeOnServer<TMessage>(Func<TMessage, Task> handler)
            where TMessage : class, IPubSubMessage
        {
            if (_proxy == null)
                throw new Exception("Not connected to server");

            _messageHub.Subscribe(handler, null);
            return _proxy.Subscribe<TMessage>();
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
        public Task<TMessage> When<TMessage>(int millisecondsTimeout, Predicate<TMessage> match = null,
            CancellationToken cancellationToken = default)
            where TMessage : class, IPubSubMessage
        {
            return _requestResponseProcessor.When(millisecondsTimeout, match, cancellationToken);
        }

        /// <summary>
        /// Publishes the message and waits until the response message will arrive
        /// </summary>
        /// <typeparam name="TResponse">Type of the response message to wait for</typeparam>
        /// <param name="message">An instance of any class derived from IPubSubMessage</param>
        /// <param name="millisecondsTimeout">Time interval during which the response message must be received otherwise the TimeoutException will be thrown</param>
        /// <param name="cancellationToken">The cancellation token that can be used to discard awaiting the response message</param>
        /// <returns>A Task that can be awaited until the response message has been arrived</returns>
        public Task<TResponse> Process<TResponse>(IPubSubMessage message, int millisecondsTimeout,
            Predicate<TResponse> match = null, CancellationToken cancellationToken = default)
            where TResponse : class, IPubSubMessage
        {
            return _requestResponseProcessor.Process(message, millisecondsTimeout, match, cancellationToken);
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
