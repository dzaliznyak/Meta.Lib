using Meta.Lib.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class DelayedMessages
    {
        class DelayedMessageScope
        {
            public IPubSubMessage Message { get; set; }
            public TaskCompletionSource<bool> Tcs { get; set; }
            public bool IsTimedOut { get; set; }
        }

        readonly ConcurrentDictionary<Type, ConcurrentQueue<DelayedMessageScope>> _dict =
            new ConcurrentDictionary<Type, ConcurrentQueue<DelayedMessageScope>>();

        internal async Task Put(IPubSubMessage message)
        {
            var queue = _dict.GetOrAdd(message.GetType(), m => new ConcurrentQueue<DelayedMessageScope>());

            var scope = new DelayedMessageScope
            {
                Message = message,
                Tcs = new TaskCompletionSource<bool>()
            };
            queue.Enqueue(scope);

            try
            {
                await scope.Tcs.Task.TimeoutAfter(message.WaitForSubscriberTimeout);
            }
            catch (TimeoutException)
            {
                scope.IsTimedOut = true;
                throw;
            }
        }

        internal void OnNewSubscriber(Type messageType, ISubscription subscriber)
        {
            Task.Run(async () =>
            {
                if (_dict.TryGetValue(messageType, out var queue))
                {
                    List<DelayedMessageScope> filteredMessages = null;
                    while (queue.TryDequeue(out DelayedMessageScope scope))
                    {
                        try
                        {
                            if (!scope.IsTimedOut)
                            {
                                if (subscriber.ShouldDeliver(scope.Message))
                                {
                                    await subscriber.Deliver(scope.Message);
                                    scope.Tcs.SetResult(true);
                                }
                                else
                                {
                                    if (filteredMessages == null)
                                        filteredMessages = new List<DelayedMessageScope>();
                                    filteredMessages.Add(scope);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            scope.Tcs.SetException(ex);
                        }
                    }

                    // put to the queue again all messages that are not sent
                    if (filteredMessages != null)
                        foreach (var item in filteredMessages)
                            queue.Enqueue(item);
                }
            });
        }

        internal void OnNewPipeSubscriber(Type messageType, PipeServer pipe)
        {
            Task.Run(async () =>
            {
                if (_dict.TryGetValue(messageType, out var queue))
                {
                    List<DelayedMessageScope> filteredMessages = null;
                    while (queue.TryDequeue(out DelayedMessageScope scope))
                    {
                        try
                        {
                            if (!scope.IsTimedOut)
                            {
                                if (pipe.IsShouldSend(scope.Message) &&
                                    await pipe.SendMessage(scope.Message, PipeMessageType.Message))
                                {
                                    scope.Tcs.SetResult(true);
                                }
                                else
                                {
                                    if (filteredMessages == null)
                                        filteredMessages = new List<DelayedMessageScope>();
                                    filteredMessages.Add(scope);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            scope.Tcs.SetException(ex);
                        }
                    }

                    // put to the queue again all messages that are not sent
                    if (filteredMessages != null)
                        foreach (var item in filteredMessages)
                            queue.Enqueue(item);
                }
            });
        }

    }
}
