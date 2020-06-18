using Meta.Lib.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class DeliveryManager
    {
        readonly Func<IPubSubMessage, Task> _onDelayedMessage;
        readonly Func<IPubSubMessage, Task<bool>> _onRemoteDeliver;

        public DeliveryManager(Func<IPubSubMessage, Task> onDelayedMessage,
                               Func<IPubSubMessage, Task<bool>> onRemoteDeliver)
        {
            _onDelayedMessage = onDelayedMessage;
            _onRemoteDeliver = onRemoteDeliver;
        }

        internal async Task Put(IReadOnlyCollection<Subscriber> subscribers, IPubSubMessage message)
        {
            bool hasAtLeastOneSubscriber = false;
            List<Exception> exceptions = null;

            // deliver to local subscribers
            foreach (var item in subscribers)
            {
                try
                {
                    if (item.Subscription.ShouldDeliver(message))
                    {
                        hasAtLeastOneSubscriber = true;
                        await item.Subscription.Deliver(message);
                    }
                }
                catch (Exception ex)
                {
                    if (exceptions == null)
                        exceptions = new List<Exception>();
                    exceptions.Add(ex);
                }
            }

            // deliver to remote subscribers
            try
            {
                if (await _onRemoteDeliver(message))
                    hasAtLeastOneSubscriber = true;
            }
            catch (Exception ex)
            {
                if (exceptions == null)
                    exceptions = new List<Exception>();
                exceptions.Add(ex);
            }

            if (exceptions != null)
                throw new AggregateException(exceptions).Fix();

            if (message.DeliverAtLeastOnce && !hasAtLeastOneSubscriber)
            {
                if (message.Timeout > 0)
                    await _onDelayedMessage(message);
                else
                    throw new NoSubscribersException("Failed to deliver the message - no one is listening");
            }
        }
    }
}
