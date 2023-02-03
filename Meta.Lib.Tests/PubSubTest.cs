using Meta.Lib.Examples.Shared;
using Meta.Lib.Modules.PubSub;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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
        Task OnMyMessageHandler(MyMessage message)
        {
            message.DeliveredCount++;
            return Task.CompletedTask;
        }

        Task OnMyMessageHandler2(MyMessage message)
        {
            message.DeliveredCount++;
            return Task.CompletedTask;
        }

        Task OnMyMessageHandler3(MyMessage message)
        {
            message.DeliveredCount++;
            return Task.CompletedTask;
        }

        bool OnMyMessagePredicate(MyMessage message)
        {
            return message.Version > new Version(1, 0);
        }

        [TestMethod]
        // subscribed twice but delivered only once
        public async Task Basic()
        {
            var hub = new MetaPubSub();

            var message = new MyMessage();
            await hub.Publish(message);
            Assert.IsTrue(message.DeliveredCount == 0);

            hub.Subscribe<MyMessage>(OnMyMessageHandler);
            hub.Subscribe<MyMessage>(OnMyMessageHandler);

            message = new MyMessage();
            await hub.Publish(message);
            Assert.IsTrue(message.DeliveredCount == 1);

            await hub.Unsubscribe<MyMessage>(OnMyMessageHandler);

            message = new MyMessage();
            await hub.Publish(message);
            Assert.IsTrue(message.DeliveredCount == 0);
        }


        [TestMethod]
        public async Task Predicate()
        {
            var hub = new MetaPubSub();

            hub.Subscribe<MyMessage>(OnMyMessageHandler, OnMyMessagePredicate);

            var message = new MyMessage { Version = new Version(1, 0) };
            await hub.Publish(message);
            Assert.IsTrue(message.DeliveredCount == 0);

            message = new MyMessage { Version = new Version(1, 1) };
            await hub.Publish(message);
            Assert.IsTrue(message.DeliveredCount == 1);

            await hub.Unsubscribe<MyMessage>(OnMyMessageHandler);
        }

        [TestMethod]
        // Should get the NoSubscribersException if message is filtered and DeliverAtLeastOnce = true
        public async Task Test_DeliverAtLeastOnce_Filtered()
        {
            var hub = new MetaPubSub();
            bool noSubscriberException = false;

            hub.Subscribe<MyMessage>(OnMyMessageHandler, OnMyMessagePredicate);

            var message = new MyMessage { Version = new Version(1, 0), DeliverAtLeastOnce = true };
            try
            {
                await hub.Publish(message);
            }
            catch (NoSubscribersException)
            {
                noSubscriberException = true;
            }
            Assert.IsTrue(noSubscriberException);
            Assert.IsTrue(message.DeliveredCount == 0);

            await hub.Unsubscribe<MyMessage>(OnMyMessageHandler);
        }

        [TestMethod]
        // Should deliver if message is filtered at first and DeliverAtLeastOnce = true but Timeout > 0 and 
        // after the message has published a new subscriber arrived
        public async Task Test_DeliverAtLeastOnce_Delayed()
        {
            var hub = new MetaPubSub();

            // first subscriber which will not process the message due to its filter
            hub.Subscribe<MyMessage>(OnMyMessageHandler, OnMyMessagePredicate);

            // second subscriber which will subscribe after the message has been published 
            // also with filter, will not process the message
            var t = Task.Run(async () =>
            {
                await Task.Delay(50);
                hub.Subscribe<MyMessage>(OnMyMessageHandler2, OnMyMessagePredicate);
            });

            // third subscriber which will subscribe after the message has been published 
            var t2 = Task.Run(async () =>
            {
                await Task.Delay(70);
                hub.Subscribe<MyMessage>(OnMyMessageHandler3);
            });

            var message = new MyMessage { Version = new Version(1, 0), DeliverAtLeastOnce = true, WaitForSubscriberTimeout = 200000 };
            // the message has a timeout and can wait until the second subscriber come
            await hub.Publish(message);

            Assert.IsTrue(message.DeliveredCount == 1);

            await hub.Unsubscribe<MyMessage>(OnMyMessageHandler);
            //hub.Unsubscribe<MyMessage>(OnMyMessageHandler2);
            //hub.Unsubscribe<MyMessage>(OnMyMessageHandler3);
        }


        [TestMethod]
        // same as previous but should not deliver the message 
        // because the second subscriber has also the same filter
        public async Task Test_DeliverAtLeastOnce_Delayed2()
        {
            var hub = new MetaPubSub();
            bool timeoutException = false;

            // first subscriber which will not process the message due to its filter
            hub.Subscribe<MyMessage>(OnMyMessageHandler, OnMyMessagePredicate);

            // second subscriber which will subscribe after the message has been published
            var t = Task.Run(async () =>
            {
                await Task.Delay(50);
                hub.Subscribe<MyMessage>(OnMyMessageHandler2, OnMyMessagePredicate);
                await hub.Unsubscribe<MyMessage>(OnMyMessageHandler2);
            });

            var message = new MyMessage { Version = new Version(1, 0), DeliverAtLeastOnce = true, WaitForSubscriberTimeout = 100 };
            try
            {
                // the message has a timeout and can wait until the second subscriber come
                await hub.Publish(message);
            }
            catch (TimeoutException)
            {
                timeoutException = true;
            }
            Assert.IsTrue(timeoutException);
            Assert.IsTrue(message.DeliveredCount == 0);

            await hub.Unsubscribe<MyMessage>(OnMyMessageHandler);
        }

        [TestMethod]
        public async Task Test_When()
        {
            var hub = new MetaPubSub();

            var t = Task.Run(async () =>
            {
                await Task.Delay(50);
                await hub.Publish(new MyEvent());
            });

            var res = await hub.When<MyEvent>(100);
            Assert.IsNotNull(res);

            bool timeoutException = false;
            try
            {
                res = await hub.When<MyEvent>(100);
            }
            catch (TimeoutException)
            {
                timeoutException = true;
            }
            Assert.IsTrue(timeoutException);

        }

        [TestMethod]
        public async Task Test_Cancel_When()
        {
            var hub = new MetaPubSub();
            var cts = new CancellationTokenSource();

            // publish an event after 100 ms
            var t = Task.Run(async () =>
            {
                await Task.Delay(100);
                await hub.Publish(new MyEvent());
            });

            // cancel waiting after 50 ms
            var t2 = Task.Run(async () =>
            {
                await Task.Delay(50);
                cts.Cancel();
            });


            bool exception = false;
            try
            {
                var res = await hub.When<MyEvent>(200, null, cts.Token);
                Assert.IsTrue(false);
            }
            catch (OperationCanceledException)
            {
                exception = true;
            }
            Assert.IsTrue(exception);
        }

        [TestMethod]
        public async Task Test_Process()
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

            var message = new MyMessage { ResponseTimeout = 2000 };
            var res = await hub.Process<MyEvent>(message);
            Assert.IsNotNull(res);
        }

        [TestMethod]
        public async Task Test_MultiProcess()
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
                        ResponseTimeout = 1000
                    };
                    var res = await hub.Process<MyEvent>(message, x => x.SomeId == i);
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
                        SomeId = i,
                        ResponseTimeout = 1000
                    };
                    var res = await hub.Process<MyEvent>(message, x => x.SomeId == i);
                    Assert.IsNotNull(res);
                    Assert.IsTrue(res.SomeId == i);
                }
            });

            await Task.WhenAll(t1, t2);
        }

        [TestMethod]
        public async Task Test_Schedule()
        {
            var hub = new MetaPubSub();
            bool received = false;

            Task Handler(MyMessage x)
            {
                received = true;
                return Task.CompletedTask;
            }

            hub.Subscribe<MyMessage>(Handler);

            var message = new MyMessage { Version = new Version(1, 0), DeliverAtLeastOnce = true, WaitForSubscriberTimeout = 100 };
            hub.Schedule(message, 100);

            await Task.Delay(50);
            Assert.IsFalse(received);
            GC.Collect();

            await Task.Delay(60);
            Assert.IsTrue(received);
        }

    }
}
