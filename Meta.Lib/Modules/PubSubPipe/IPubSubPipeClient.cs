using Meta.Lib.Modules.PubSub;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSubPipe
{
    /// <summary>
    /// Client pipe connection to the server PubSub
    /// </summary>
    public interface IPubSubPipeClient
    {
        bool IsConnected { get; }
        IMetaPubSub MetaPubSub { get; }

        event EventHandler Connected;
        event EventHandler Disconnected;

        /// <summary>
        /// Connect to a server pipe.
        /// </summary>
        Task ConnectToServer();

        /// <summary>
        /// Trying to connect to a server pipe. If connection times out starts a reconnection loop and returns false.
        /// </summary>
        Task<bool> TryConnectToServer();

        /// <summary>
        /// Disconnects from the server pipe.
        /// </summary>
        void DisconnectFromServer();

        /// <summary>
        /// Subscribes to a message of TMessage type on the server. Use UnsubscribeOnServer to stop receiving messages of TMessage type. 
        /// Calling this method again increments the counter on the server side, so for every call to SubscribeOnServer there must be a corresponding call to UnsubscribeOnServer.
        /// </summary>
        /// <typeparam name="TMessage">Type of a message to subscribe to</typeparam>
        /// <exception cref="NotConnectedToServerException" />
        Task SubscribeOnServer<TMessage>();

        /// <summary>
        /// Decreases the reference counter on the server side and when it reaches zero stops receiving messages of type TMessage.
        /// For every call to SubscribeOnServer there must be a corresponding call to UnsubscribeOnServer.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <returns></returns>
        /// <exception cref="NotConnectedToServerException"></exception>
        Task UnsubscribeOnServer<TMessage>();

        /// <summary>
        /// This method subscribes to a message of type TMessage on the server. 
        /// If the server is not currently connected, it will return false. 
        /// The subscription will be established automatically once the server is connected.
        /// </summary>
        /// <typeparam name="TMessage">Type of a message to subscribe to</typeparam>
        /// <returns>Returns false if the server is not connected otherwise returns true.</returns>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="InvalidOperationException" />
        Task<bool> TrySubscribeOnServer<TMessage>();

        /// <summary>
        /// Publishes a message on the server
        /// </summary>
        /// <param name="message">An instance of any class</param>
        /// <param name="options">This refers to an optional parameter that contains the parameters used for sending the message.</param>
        /// <returns>A Task that can be awaited until the message has been processed by all subscribers</returns>
        /// <exception cref="AggregateException" />
        /// <exception cref="TimeoutException" />
        /// <exception cref="NoSubscribersException" />
        /// <exception cref="NotConnectedToServerException" />
        /// <exception cref="ArgumentException" />
        Task PublishOnServer<TMessage>(TMessage message, PubSubOptions options = null);

        /// <summary>
        /// Publishes the message and waits until the response message will arrive
        /// </summary>
        /// <typeparam name="TResponse">Type of the response message to wait for</typeparam>
        /// <param name="message">An instance of any class</param>
        /// <param name="responseTimeoutMs">Период времени, в течении которого ответ должен поступить. В противном случае будет получен TimeoutException.</param>
        /// <param name="options">This refers to an optional parameter that contains the parameters used for sending the message.</param>
        /// <param name="cancellationToken">The cancellation token that can be used to discard awaiting the response message</param>
        /// <returns>A Task that can be awaited until the response message has been arrived</returns>
        /// <exception cref="TimeoutException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="ArgumentException"></exception>
        Task<TResponse> ProcessOnServer<TMessage, TResponse>(
            TMessage message,
            int responseTimeoutMs = 5000,
            PubSubOptions options = null,
            CancellationToken cancellationToken = default);

        void WaitForReconnect(int millisecondsTimeout);
    }
}
