using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public interface IMetaPubSub
    {
        Task ConnectToServer(string pipeName, string serverName = ".");

        void DisconnectFromServer();

        void StartServer(string pipeName);

        void StopServer();

        void Subscribe<TMessage>(Func<TMessage, Task> handler, Predicate<TMessage> match)
            where TMessage : class, IPubSubMessage;

        Task SubscribeOnServer<TMessage>(Func<TMessage, Task> handler)
            where TMessage : class, IPubSubMessage;

        Task Unsubscribe<TMessage>(Func<TMessage, Task> handler)
            where TMessage : class, IPubSubMessage;

        Task Publish(IPubSubMessage message);

        Task PublishOnServer(IPubSubMessage message);

        Task<TMessage> When<TMessage>(int millisecondsTimeout, Predicate<TMessage> match = null, 
            CancellationToken cancellationToken = default)
            where TMessage : class, IPubSubMessage;

        Task<TResponse> Process<TResponse>(IPubSubMessage message, int millisecondsTimeout, 
            Predicate<TResponse> match = null, CancellationToken cancellationToken = default)
            where TResponse : class, IPubSubMessage;

        void Schedule(IPubSubMessage message, int millisecondsDelay, CancellationToken cancellationToken = default);
    }
}
