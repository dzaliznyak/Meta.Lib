using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public partial class MetaPubSub : IMetaPubSub
    {
        /// <summary>
        /// Name of the server pipe. Null if the server hasn't been started.
        /// </summary>
        public string PipeName => _pipeConections.PipeName;

        /// <summary>
        /// Shows is this hub connected to another server hub.
        /// </summary>
        public bool IsConnectedToServer => _proxy.IsConnected;

        /// <summary>
        /// Connect to a server hub.
        /// </summary>
        /// <param name="pipeName">Name of the pipe opened on the server.</param>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait for the server to respond before the connection times out</param>
        /// <param name="serverName">Name of the computer where the server runs. If the server and local hub on the same computer use "."</param>
        /// <exception cref="TimeoutException"/>
        public Task ConnectToServer(string pipeName, int millisecondsTimeout = 5_000, string serverName = ".")
        {
            return _proxy.Connect(pipeName, millisecondsTimeout, reconnectionPeriod: 0, serverName);
        }

        /// <summary>
        /// Trying to connect to a server hub. If connection times out starts a reconnection loop and returns false.
        /// </summary>
        /// <param name="pipeName">Name of the pipe opened on the server.</param>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait for the server to respond before the connection times out</param>
        /// <param name="reconnectionPeriod">The time delay between attempts to establish the connection.</param>
        /// <param name="serverName">Name of the computer where the server runs. If the server and local hub on the same computer use "."</param>
        /// <returns>Returns true if the connection has been established.</returns>
        public Task<bool> TryConnectToServer(string pipeName, int millisecondsTimeout = 1_000, int reconnectionPeriod = 5_000, string serverName = ".")
        {
            return _proxy.Connect(pipeName, millisecondsTimeout, reconnectionPeriod, serverName);
        }

        /// <summary>
        /// Disconnects from the server hub.
        /// </summary>
        public void DisconnectFromServer()
        {
            _proxy.Disconnect();
        }

        /// <summary>
        /// Starts accepting for incoming client connections.
        /// </summary>
        /// <param name="pipeName">Unique name for this server. The same name should be used to call ConnectToServer() method.</param>
        /// <param name="configure">Delegate wich can be used to create NamedPipeServerStream with non-default parameters.</param>
        public void StartServer(string pipeName, Func<NamedPipeServerStream> configure = null)
        {
            if (_proxy.ConnectedOrConnecting)
                throw new InvalidOperationException("Cannot be started as a server when has been connected to another server");

            _pipeConections.Start(pipeName, configure);
        }

        /// <summary>
        /// Disconnects all client connections and stops accepting new connections.
        /// </summary>
        public void StopServer()
        {
            _pipeConections.Stop();
        }

        /// <summary>
        /// Subscribe to a message of TMessage type
        /// </summary>
        /// <typeparam name="TMessage">Type of a message to subscribe to</typeparam>
        /// <param name="handler">Client defined function that will be called when the message has arrived</param>
        /// <param name="match">The delegate that defines the conditions of the message to subscribe for. If null all messages of the specified type will be received</param>
        /// <exception cref="ArgumentNullException" />
        public void Subscribe<TMessage>(Func<TMessage, Task> handler, Predicate<TMessage> match = null)
            where TMessage : class, IPubSubMessage
        {
            _messageHub.Subscribe(handler, match);
        }

        /// <summary>
        /// Subscribes to a message of TMessage type on the server and locally.
        /// </summary>
        /// <typeparam name="TMessage">Type of a message to subscribe to</typeparam>
        /// <param name="handler">Client defined function that will be called when the message has arrived</param>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="InvalidOperationException" />
        public Task SubscribeOnServer<TMessage>(Func<TMessage, Task> handler)
            where TMessage : class, IPubSubMessage
        {
            if (!_proxy.ConnectedOrConnecting)
                throw new InvalidOperationException("Not connected to a server");

            _messageHub.Subscribe(handler, null);
            return _proxy.Subscribe<TMessage>();
        }

        /// <summary>
        /// Subscribes to a message of TMessage type locally and then trying to subscribe on the server. If the server not connected returns false.
        /// Subscription will be made automatically after the server connection.
        /// </summary>
        /// <typeparam name="TMessage">Type of a message to subscribe to</typeparam>
        /// <param name="handler">Client defined function that will be called when the message has arrived</param>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="InvalidOperationException" />
        public Task<bool> TrySubscribeOnServer<TMessage>(Func<TMessage, Task> handler)
            where TMessage : class, IPubSubMessage
        {
            _messageHub.Subscribe(handler, null);
            return _proxy.TrySubscribe<TMessage>();
        }

        /// <summary>
        /// Unsubscribe from a message of TMessage type
        /// </summary>
        /// <typeparam name="TMessage">Type of message to unsubscribe from</typeparam>
        /// <param name="handler">The same callback function that was passed to the Subscribe method</param>
        public Task Unsubscribe<TMessage>(Func<TMessage, Task> handler)
            where TMessage : class, IPubSubMessage
        {
            _messageHub.Unsubscribe(handler);
            //return Task.CompletedTask;

            //if (_proxy != null)
            return _proxy.Unsubscribe(typeof(TMessage));
            //else
            //     return Task.CompletedTask;
        }

        /// <summary>
        /// Publish a message
        /// </summary>
        /// <param name="message">An instance of any class derived from IPubSubMessage</param>
        /// <returns>A Task that can be awaited until the message has been processed by all subscribers</returns>
        /// <exception cref="AggregateException" />
        /// <exception cref="TimeoutException" />
        /// <exception cref="NoSubscribersException" />
        public Task Publish(IPubSubMessage message)
        {
            return _messageHub.Publish(message);
        }

        public Task PublishOnServer(IPubSubMessage message)
        {
            //  if (_proxy == null)
            //      throw new Exception("Not connected to server");

            return _proxy.SendMessage(message, PipeMessageType.Message);
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

        public Task<TResponse> ProcessOnServer<TResponse>(IPubSubMessage message, int millisecondsTimeout,
            CancellationToken cancellationToken = default)
            where TResponse : class, IPubSubMessage
        {
            return _requestResponseProcessor.ProcessOnServer<TResponse>(message, millisecondsTimeout, cancellationToken);
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
