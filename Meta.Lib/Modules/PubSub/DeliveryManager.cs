using Meta.Lib.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class DeliveryManager
    {
        readonly ILogger _logger;
        readonly Func<IPubSubMessage, Task> _onDelayedMessage;
        //readonly Func<IPubSubMessage, Task<bool>> _onRemoteDeliver;

        public DeliveryManager(ILogger logger,
                               Func<IPubSubMessage, Task> onDelayedMessage
                               /*Func<IPubSubMessage, Task<bool>> onRemoteDeliver*/)
        {
            _logger = logger;
            _onDelayedMessage = onDelayedMessage;
           // _onRemoteDeliver = onRemoteDeliver;
        }

        internal async Task Put(IReadOnlyCollection<ISubscription> subscribers, IPubSubMessage message)
        {
            bool hasAtLeastOneSubscriber = false;
            List<Exception> exceptions = null;

            // deliver to local subscribers
            foreach (var item in subscribers)
            {
                try
                {
                    if (item.ShouldDeliver(message))
                    {
                        hasAtLeastOneSubscriber = true;
                        await item.Deliver(message);
                    }
                }
                catch (Exception ex)
                {
                    if (exceptions == null)
                        exceptions = new List<Exception>();
                    exceptions.Add(ex);
                }
            }

            //// deliver to remote subscribers
            //bool remoteDeliver = false;
            //try
            //{
            //    if (await _onRemoteDeliver(message))
            //        remoteDeliver = hasAtLeastOneSubscriber = true;
            //}
            //catch (Exception ex)
            //{
            //    if (exceptions == null)
            //        exceptions = new List<Exception>();
            //    exceptions.Add(ex);
            //}

            if (exceptions != null)
                throw new AggregateException(exceptions).Fix();

            if (message.DeliverAtLeastOnce && !hasAtLeastOneSubscriber)
            {
                if (message.WaitForSubscriberTimeout > 0)
                {
                    _logger?.LogDebug("Delayed <<<{Message}>>> for {Timeout} ms", message.GetType().Name, message.WaitForSubscriberTimeout);
                    await _onDelayedMessage(message);
                }
                else
                {
                    throw new NoSubscribersException("Failed to deliver the message - no one is listening");
                }
            }
        }
    }
}
