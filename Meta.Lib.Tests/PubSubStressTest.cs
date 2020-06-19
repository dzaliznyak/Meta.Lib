using Meta.Lib.Examples.Shared;
using Meta.Lib.Modules.Logger;
using Meta.Lib.Modules.PubSub;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Meta.Lib.Tests
{
    [TestClass]
    public class PubSubStressTest
    {

        [TestMethod]
        // subscribed twice but delivered only once
        public async Task Basic()
        {
            var hub = new MetaPubSub();


            await Task.Run(() =>
            {
                for (int i = 0; i < 10_000; i++)
                {
                    var subscriber = new MySubscriber();
                    //await Task.Delay(50);
                    hub.Subscribe<MyMessage>(subscriber.Handler);
                }
            });

            int totalDeliveryCount1 = 0;
            var t1 = Task.Run(async () =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    var subscriber = new MySubscriber();
                    hub.Subscribe<MyEvent>(subscriber.Handler);

                    var message = new MyMessage();
                    await hub.Publish(message);
                    totalDeliveryCount1 += message.DeliveredCount;

                    hub.Unsubscribe<MyEvent>(subscriber.Handler);
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

            Assert.IsTrue(totalDeliveryCount1 > (10_000 * 1000) && totalDeliveryCount1 < (10_000 * 1000 + 1000));
            Assert.IsTrue(totalDeliveryCount2 == 10_000 * 1000 + 1000);
        }
    }
}
