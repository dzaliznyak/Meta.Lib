using Meta.Lib.Examples.Shared;
using Meta.Lib.Modules.PubSub;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Meta.Lib.Tests
{
    [TestClass]
    public class PubSubStressTest
    {

        [TestMethod]
        public async Task Basic()
        {
            var hub = new MetaPubSub();

            for (int i = 0; i < 10_000; i++)
            {
                var subscriber = new MySubscriber();
                hub.Subscribe<MyMessage>(subscriber.Handler);
            }

            int totalDeliveryCount1 = 0;
            var t1 = Task.Run(async () =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    var message = new MyMessage();
                    await hub.Publish(message);
                    totalDeliveryCount1 += message.DeliveredCount;
                }
            });

            int totalDeliveryCount2 = 0;
            var t2 = Task.Run(async () =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    var subscriber = new MySubscriber();
                    hub.Subscribe<MyMessage>(subscriber.Handler);

                    var message = new MyMessage();
                    await hub.Publish(message);
                    totalDeliveryCount2 += message.DeliveredCount;

                    hub.Unsubscribe<MyMessage>(subscriber.Handler);
                }
            });

            await Task.WhenAll(t1, t2);

            Assert.IsTrue(totalDeliveryCount1 >= (10_000 * 1000) && totalDeliveryCount1 <= (10_000 * 1000 + 1000));
            Assert.IsTrue(totalDeliveryCount2 == 10_000 * 1000 + 1000);
        }

        [TestMethod]
        // two threads subscribe the same handler to the same message in a loop. 
        public async Task Test_MultiThread_Subscribe()
        {
            var hub = new MetaPubSub();

            Task Handler(MyMessage x)
            {
                x.DeliveredCount++;
                return Task.CompletedTask;
            }

            var t1 = Task.Run(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    hub.Subscribe<MyMessage>(Handler);

                    var message = new MyMessage();
                    hub.Publish(message);
                    Assert.IsTrue(message.DeliveredCount <= 1);

                    hub.Unsubscribe<MyMessage>(Handler);
                }
            });

            var t2 = Task.Run(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    hub.Subscribe<MyMessage>(Handler);
                    
                    var message = new MyMessage();
                    hub.Publish(message);
                    Assert.IsTrue(message.DeliveredCount <= 1);
                    
                    hub.Unsubscribe<MyMessage>(Handler);
                }
            });

            await Task.WhenAll(t1, t2);
        }

    }
}
