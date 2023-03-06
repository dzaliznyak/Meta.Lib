using Meta.Lib.Examples.Shared;
using Meta.Lib.Exceptions;
using Meta.Lib.Modules.PubSub;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Tests
{
    public class MySubscriber
    {
        internal Task Handler(MyMessage x)
        {
            x.DeliveredCount++;
            return Task.CompletedTask;
        }
    }

    [TestClass]
    public class PubSubTest
    {
        Task MessageHandler(MyMessage message)
        {
            message.DeliveredCount++;
            return Task.CompletedTask;
        }

        Task MessageHandler2(MyMessage message)
        {
            message.DeliveredCount++;
            return Task.CompletedTask;
        }

        Task MessageHandler3(MyMessage message)
        {
            message.DeliveredCount++;
            return Task.CompletedTask;
        }

        bool MessageFilter(MyMessage message)
        {
            return message.Version > new Version(1, 0);
        }

        [TestMethod]
        public async Task SubscribedTwice_DeliveredOnce()
        {
            var hub = new MetaPubSub();

            var message = new MyMessage();
            await hub.Publish(message);
            Assert.IsTrue(message.DeliveredCount == 0);

            hub.Subscribe<MyMessage>(MessageHandler);
            hub.Subscribe<MyMessage>(MessageHandler);

            message = new MyMessage();
            await hub.Publish(message);
            Assert.IsTrue(message.DeliveredCount == 1);

            hub.Unsubscribe<MyMessage>(MessageHandler);

            message = new MyMessage();
            await hub.Publish(message);
            Assert.IsTrue(message.DeliveredCount == 0);
        }

        [TestMethod]
        public async Task SubscribeNonGeneric()
        {
            Task Handler(object obj)
            {
                var message = (MyMessage)obj;
                message.DeliveredCount++;
                return Task.CompletedTask;
            }

            var hub = new MetaPubSub();

            hub.Subscribe(typeof(MyMessage), Handler);

            var message = new MyMessage { Version = new Version(1, 0) };
            await hub.Publish(message);
            Assert.IsTrue(message.DeliveredCount == 1);

            hub.Unsubscribe(typeof(MyMessage), Handler);

            message = new MyMessage { Version = new Version(1, 0) };
            await hub.Publish(message);
            Assert.IsTrue(message.DeliveredCount == 0);
        }


        [TestMethod]
        public async Task SubscribeWithPredicate()
        {
            var hub = new MetaPubSub();

            hub.Subscribe<MyMessage>(MessageHandler, MessageFilter);

            var message = new MyMessage { Version = new Version(1, 0) };
            await hub.Publish(message);
            Assert.IsTrue(message.DeliveredCount == 0);

            message = new MyMessage { Version = new Version(1, 1) };
            await hub.Publish(message);
            Assert.IsTrue(message.DeliveredCount == 1);

            hub.Unsubscribe<MyMessage>(MessageHandler);
        }

        [TestMethod]
        // Should get the NoSubscribersException if message is filtered and DeliverAtLeastOnce = true
        public async Task SubscribeWithPredicate_PublishWithDeliverAtLeastOnce()
        {
            var hub = new MetaPubSub();
            Exception exception = null;

            hub.Subscribe<MyMessage>(MessageHandler, MessageFilter);

            var message = new MyMessage { Version = new Version(1, 0) };
            try
            {
                await hub.Publish(message, new PubSubOptions() { DeliverAtLeastOnce = true });
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsTrue(exception is NoSubscribersException);
            Assert.IsTrue(message.DeliveredCount == 0);

            hub.Unsubscribe<MyMessage>(MessageHandler);
        }

        [TestMethod]
        // Should deliver if message is filtered at first and DeliverAtLeastOnce = true but Timeout > 0 and 
        // after the message has published a new subscriber arrived
        public async Task SubscribeAfterMessageQueued()
        {
            var hub = new MetaPubSub();

            // first subscriber which will not process the message due to its filter
            hub.Subscribe<MyMessage>(MessageHandler, MessageFilter);

            // second subscriber which will subscribe after the message has been published 
            // also with filter, will not process the message
            var t = Task.Run(async () =>
            {
                await Task.Delay(50);
                hub.Subscribe<MyMessage>(MessageHandler2, MessageFilter);
            });

            // third subscriber which will subscribe after the message has been published 
            var t2 = Task.Run(async () =>
            {
                await Task.Delay(100);
                hub.Subscribe<MyMessage>(MessageHandler3);
            });

            var message = new MyMessage { Version = new Version(1, 0) };
            // the message has a timeout and can wait until the second subscriber come
            await hub.Publish(message, new PubSubOptions() { DeliverAtLeastOnce = true, WaitForSubscriberTimeout = 1000 });

            Assert.IsTrue(message.DeliveredCount == 1);

            hub.Unsubscribe<MyMessage>(MessageHandler);
            hub.Unsubscribe<MyMessage>(MessageHandler2);
            hub.Unsubscribe<MyMessage>(MessageHandler3);
        }

        [TestMethod]
        public async Task When_SuccessfullyReceivesMessage()
        {
            var hub = new MetaPubSub();

            var t = Task.Run(async () =>
            {
                await Task.Delay(50);
                await hub.Publish(new MyEvent());
            });

            var res = await hub.When<MyEvent>(100);

            Assert.IsTrue(res is MyEvent);
        }

        [TestMethod]
        public async Task When_ReceivesTimeoutException()
        {
            var hub = new MetaPubSub();
            Exception exception = null;

            try
            {
                var res = await hub.When<MyEvent>(100);
            }
            catch (TimeoutException ex)
            {
                exception = ex;
            }

            Assert.IsTrue(exception is TimeoutException);
        }

        [TestMethod]
        public async Task CancelWhen_ReceivesOperationCanceledException()
        {
            var hub = new MetaPubSub();
            var cts = new CancellationTokenSource();
            Exception exception = null;

            var t2 = Task.Run(async () =>
            {
                await Task.Delay(50);
                // cancel waiting after 50 ms
                cts.Cancel();
            });

            try
            {
                var res = await hub.When<MyEvent>(200, null, cts.Token);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsTrue(exception is OperationCanceledException);
        }

        [TestMethod]
        public async Task Process_SuccessfullyReceivesMessage()
        {
            var hub = new MetaPubSub();

            Task Handler(MyMessage x)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(10);
                    await hub.Publish(new MyEvent());
                });
                return Task.CompletedTask;
            }

            hub.Subscribe<MyMessage>(Handler);

            var res = await hub.Process<MyMessage, MyEvent>(new MyMessage(), responseTimeoutMs: 2000);
            Assert.IsTrue(res is MyEvent);
        }

        [TestMethod]
        public async Task MultiProcess_SuccessfullyReceivesAllMessages()
        {
            var hub = new MetaPubSub();

            Task Handler(MyMessage x)
            {
                hub.Publish(new MyEvent() { SomeId = x.SomeId });
                return Task.CompletedTask;
            }

            hub.Subscribe<MyMessage>(Handler);

            var t1 = Task.Run(async () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var message = new MyMessage
                    {
                        SomeId = i,
                    };
                    var res = await hub.Process<MyMessage, MyEvent>(message, responseTimeoutMs: 1000, options: null, x => x.SomeId == i);
                    Assert.IsNotNull(res);
                    Assert.IsTrue(res.SomeId == i);
                }
            });

            var t2 = Task.Run(async () =>
            {
                for (int i = 100; i < 200; i++)
                {
                    var message = new MyMessage
                    {
                        SomeId = i
                    };
                    var res = await hub.Process<MyMessage, MyEvent>(message, responseTimeoutMs: 1000, options: null, x => x.SomeId == i);
                    Assert.IsNotNull(res);
                    Assert.IsTrue(res.SomeId == i);
                }
            });

            await Task.WhenAll(t1, t2);
        }

        [TestMethod]
        public async Task Schedule_SuccessfullyReceivesMessage()
        {
            var hub = new MetaPubSub();
            bool received = false;

            Task Handler(MyMessage x)
            {
                received = true;
                return Task.CompletedTask;
            }

            hub.Subscribe<MyMessage>(Handler);

            var message = new MyMessage { Version = new Version(1, 0) };
            hub.Schedule(message, 100, new PubSubOptions() { DeliverAtLeastOnce = true, WaitForSubscriberTimeout = 100 });

            await Task.Delay(50);
            Assert.IsFalse(received);
            GC.Collect();

            await Task.Delay(60);
            Assert.IsTrue(received);
        }

    }
}
