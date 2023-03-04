using Meta.Lib.Modules.PubSub;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSubPipe
{
    public interface IPubSubPipeClient
    {
        bool IsConnected { get; }

        Task ConnectToServer();

        Task<bool> TryConnectToServer();

        void DisconnectFromServer();

        Task SubscribeOnServer<TMessage>();

        Task UnsubscribeOnServer<TMessage>();

        Task<bool> TrySubscribeOnServer<TMessage>();

        Task PublishOnServer<TMessage>(TMessage message, PubSubOptions options = null);

        Task<TResponse> ProcessOnServer<TMessage, TResponse>(
            TMessage message,
            int responseTimeoutMs = 5000,
            PubSubOptions options = null,
            CancellationToken cancellationToken = default);

        void WaitForReconnect(int millisecondsTimeout);
    }
}
