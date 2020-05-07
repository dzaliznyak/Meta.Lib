using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class DeliveryManager
    {
        readonly Func<IPubSubMessage, Task> _onDelayedMessageOutput;

        public DeliveryManager(Func<IPubSubMessage, Task> onDelayedMessageOutput)
        {
            _onDelayedMessageOutput = onDelayedMessageOutput;
        }

        internal async Task Put(IReadOnlyCollection<Subscriber> subscribers, IPubSubMessage message)
        {
            bool hasAtLeastOneSubscriber = false;
            List<Exception> exceptions = null;
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

            if (exceptions != null)
                throw new AggregateException(exceptions);

            if (message.DeliverAtLeastOnce && !hasAtLeastOneSubscriber)
            {
                if (message.Timeout > 0)
                    await _onDelayedMessageOutput(message);
                else
                    throw new NoSubscribersException("Failed to deliver the message - no one is listening");
            }
        }
    }
}
