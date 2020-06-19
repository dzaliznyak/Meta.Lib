using Meta.Lib.Examples.Shared;
using Meta.Lib.Modules.PubSub;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Tests
{
    [TestClass]
    public class RemotePubSubTests
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

        [TestMethod]
        // subscribes on server and receives a message
        public async Task SendMessageToClient()
        {
            var _clientConnectedEvent = new ManualResetEventSlim();
            var _event = new ManualResetEventSlim();

            Task Handler(MyMessage x)
            {
                _event.Set();
                return Task.CompletedTask;
            }

            // creating remote hub
            var t = Task.Run(async () =>
            {
                var hub = new MetaPubSub();
                hub.StartServer("Meta1");

                // wait for the subscriber
                _clientConnectedEvent.Wait(5000);

                // publishing a message at the remote hub
                await hub.Publish(new MyMessage());
            });

            // local hub creation
            var hub = new MetaPubSub();
            await hub.ConnectServer("Meta1");
            await hub.SubscribeOnServer<MyMessage>(Handler);
            // delay allowing the server process the subscription request
            await Task.Delay(100);

            _clientConnectedEvent.Set();

            _event.Wait(5000);

            // unsubscribing
            hub.Unsubscribe<MyMessage>(Handler);
            Assert.IsTrue(_event.IsSet);
        }

        [TestMethod]
        public async Task NoSubscribersException()
        {
            var _clientConnectedEvent = new ManualResetEventSlim();
            var _event = new ManualResetEventSlim();

            // creating remote hub
            var t = Task.Run(async () =>
            {
                var hub = new MetaPubSub();
                hub.StartServer("Meta2");

                // wait for the subscriber
                _clientConnectedEvent.Wait(5000);

                try
                {
                    // publishing a message at the remote hub
                    await hub.Publish(new MyMessage() { DeliverAtLeastOnce = true });
                }
                catch (NoSubscribersException)
                {
                    _event.Set();
                }
            });

            // local hub creation
            var hub = new MetaPubSub();
            await hub.ConnectServer("Meta2");
            // delay allowing the server process the connection
            await Task.Delay(100);
            _clientConnectedEvent.Set();

            _event.Wait(5000);

            Assert.IsTrue(_event.IsSet);
        }

        [TestMethod]
        public async Task DelayedMessage()
        {
            var _event = new ManualResetEventSlim();

            Task Handler(MyMessage x)
            {
                _event.Set();
                return Task.CompletedTask;
            }

            // creating remote hub
            var t = Task.Run(async () =>
            {
                var hub = new MetaPubSub();
                hub.StartServer("Meta3");

                // publishing a message at the remote hub
                await hub.Publish(new MyMessage() { DeliverAtLeastOnce = true, Timeout = 20_000 });
            });

            await Task.Delay(1_000);

            // local hub creation
            var hub = new MetaPubSub();
            await hub.ConnectServer("Meta3");
            await hub.SubscribeOnServer<MyMessage>(Handler);

            _event.Wait(5_000);

            // unsubscribing
            hub.Unsubscribe<MyMessage>(Handler);
            Assert.IsTrue(_event.IsSet);
        }

        [TestMethod]
        public Task SendMessageToServer()
        {
            return Task.CompletedTask;
        }

        [TestMethod]
        public Task ReconnectToServer()
        {
            return Task.CompletedTask;
        }

    }
}
