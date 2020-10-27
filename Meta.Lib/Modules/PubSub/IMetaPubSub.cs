using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public interface IMetaPubSub
    {
        Task ConnectToServer(string pipeName, int millisecondsTimeout = 5_000, string serverName = ".");

        Task<bool> TryConnectToServer(string pipeName, int millisecondsTimeout = 1_000, int reconnectionPeriod = 5_000, string serverName = ".");

        Task DisconnectFromServer();

        void StartServer(string pipeName, Func<NamedPipeServerStream> configure = null);

        void StopServer();

        void Subscribe<TMessage>(Func<TMessage, Task> handler, Predicate<TMessage> match = null)
            where TMessage : class, IPubSubMessage;

        Task SubscribeOnServer<TMessage>(Func<TMessage, Task> handler, Predicate<TMessage> match = null)
            where TMessage : class, IPubSubMessage;

        Task<bool> TrySubscribeOnServer<TMessage>(Func<TMessage, Task> handler, Predicate<TMessage> match = null)
            where TMessage : class, IPubSubMessage;

        Task Unsubscribe<TMessage>(Func<TMessage, Task> handler)
                    where TMessage : class, IPubSubMessage;

        Task Publish(IPubSubMessage message);

        Task PublishOnServer(IPubSubMessage message);

        Task<TMessage> When<TMessage>(int millisecondsTimeout, Predicate<TMessage> match = null,
            CancellationToken cancellationToken = default)
            where TMessage : class, IPubSubMessage;

        Task<TResponse> Process<TResponse>(IPubSubMessage message,
            Predicate<TResponse> match = null, CancellationToken cancellationToken = default)
            where TResponse : class, IPubSubMessage;

        Task<TResponse> ProcessOnServer<TResponse>(IPubSubMessage message,
            Predicate<TResponse> match = null, CancellationToken cancellationToken = default)
            where TResponse : class, IPubSubMessage;

        void Schedule(IPubSubMessage message, int millisecondsDelay, CancellationToken cancellationToken = default);
    }
}
