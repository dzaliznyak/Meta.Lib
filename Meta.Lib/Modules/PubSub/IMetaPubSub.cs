using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public interface IMetaPubSub
    {
        void Subscribe<TMessage>(Func<TMessage, Task> handler, Predicate<TMessage> match = null);

        Task Unsubscribe<TMessage>(Func<TMessage, Task> handler);

        Task Publish<TMessage>(TMessage message, PubSubOptions options = null);

        Task<TMessage> When<TMessage>(int millisecondsTimeout, 
            Predicate<TMessage> match = null,
            CancellationToken cancellationToken = default);

        Task<TResponse> Process<TMessage, TResponse>(TMessage message, 
            int responseTimeoutMs = 5000, 
            PubSubOptions options = null,
            Predicate<TResponse> match = null, 
            CancellationToken cancellationToken = default);

        void Schedule<TMessage>(TMessage message, 
            int millisecondsDelay, 
            PubSubOptions options = null, 
            CancellationToken cancellationToken = default);
    }
}
