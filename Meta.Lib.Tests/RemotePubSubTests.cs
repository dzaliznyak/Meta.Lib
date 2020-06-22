using Meta.Lib.Examples.Shared;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSub.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Tests
{
    [TestClass]
    public class RemotePubSubTests
    {
        [TestMethod]
        // subscribes on server and receives a message
        public async Task SendMessageToClient()
        {
            var pipeName = Guid.NewGuid().ToString();
            var clientConnectedEvent = new ManualResetEventSlim();
            var @event = new ManualResetEventSlim();
            int recvCount = 0;

            Task Handler(MyMessage x)
            {
                if (++recvCount == 10)
                    @event.Set();
                return Task.CompletedTask;
            }

            // creating remote hub
            var t = Task.Run(async () =>
            {
                var hub = new MetaPubSub();
                hub.StartServer(pipeName);

                // wait for the subscriber
                clientConnectedEvent.Wait(5000);

                // publishing a message at the remote hub
                for (int i = 0; i < 10; i++)
                {
                    await hub.Publish(new MyMessage());
                }
            });

            // local hub creation
            var hub = new MetaPubSub();
            await hub.ConnectServer(pipeName);
            await hub.SubscribeOnServer<MyMessage>(Handler);
            // delay allowing the server to process the subscription request
            await Task.Delay(100);

            clientConnectedEvent.Set();

            @event.Wait(5000);

            // unsubscribing
            await hub.Unsubscribe<MyMessage>(Handler);
            Assert.IsTrue(@event.IsSet && recvCount == 10);
        }

        [TestMethod]
        public async Task NoSubscribersException()
        {
            var pipeName = Guid.NewGuid().ToString();
            var clientConnectedEvent = new ManualResetEventSlim();
            var @event = new ManualResetEventSlim();

            // creating remote hub
            var t = Task.Run(async () =>
            {
                var hub = new MetaPubSub();
                hub.StartServer(pipeName);

                // wait for the subscriber
                clientConnectedEvent.Wait(5000);

                try
                {
                    // publishing a message at the remote hub
                    await hub.Publish(new MyMessage() { DeliverAtLeastOnce = true });
                }
                catch (NoSubscribersException)
                {
                    @event.Set();
                }
            });

            // local hub creation
            var hub = new MetaPubSub();
            await hub.ConnectServer(pipeName);
            // delay allowing the server process the connection
            await Task.Delay(100);
            clientConnectedEvent.Set();

            @event.Wait(5000);

            Assert.IsTrue(@event.IsSet);
        }

        [TestMethod]
        public async Task DelayedMessage()
        {
            var pipeName = Guid.NewGuid().ToString();
            var @event = new ManualResetEventSlim();

            Task Handler(MyMessage x)
            {
                @event.Set();
                return Task.CompletedTask;
            }

            var serverHub = new MetaPubSub();
            // creating remote hub
            var t = Task.Run(async () =>
            {
                serverHub.StartServer(pipeName);

                // publishing a message at the remote hub
                await serverHub.Publish(new MyMessage() { DeliverAtLeastOnce = true, Timeout = 20_000 });
            });

            await Task.Delay(1_000);

            // local hub creation
            var hub = new MetaPubSub();
            await hub.ConnectServer(pipeName);
            await hub.SubscribeOnServer<MyMessage>(Handler);

            @event.Wait(5_000);

            // unsubscribing
            await hub.Unsubscribe<MyMessage>(Handler);
            Assert.IsTrue(@event.IsSet);
        }

        [TestMethod]
        public void SendMessageToServer()
        {
            var pipeName = Guid.NewGuid().ToString();
            var clientConnectedEvent = new ManualResetEventSlim();
            var @event = new ManualResetEventSlim();
            string text = "hello\t there\n";

            Task Handler(MyMessage x)
            {
                Assert.IsTrue(x.Message == text);
                @event.Set();
                return Task.CompletedTask;
            }

            var t = Task.Run(async () =>
            {
                // local hub creation
                var hub = new MetaPubSub();
                await hub.ConnectServer(pipeName);

                try
                {
                    // publishing a message at the remote hub
                    await hub.PublishOnServer(new MyMessage(text));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                // unsubscribing
                await hub.Unsubscribe<MyMessage>(Handler);
            });

            // creating server
            var hub = new MetaPubSub();
            hub.StartServer(pipeName);
            hub.Subscribe<MyMessage>(Handler);

            @event.Wait(5_000);

            Assert.IsTrue(@event.IsSet);
        }

        [TestMethod]
        public async Task UnsubscribeFromServer()
        {
            var pipeName = Guid.NewGuid().ToString();
            var clientConnectedEvent = new ManualResetEventSlim();
            var @event = new ManualResetEventSlim();

            // local hub creation
            var hub = new MetaPubSub();

            Task Handler(MyMessage x)
            {
                hub.Unsubscribe<MyMessage>(Handler);
                return Task.CompletedTask;
            }

            // creating remote hub
            var t = Task.Run(async () =>
            {
                var serverHub = new MetaPubSub();
                serverHub.StartServer(pipeName);

                clientConnectedEvent.Wait();
                // publishing a message at the remote hub
                await serverHub.Publish(new MyMessage() { DeliverAtLeastOnce = true });

                await Task.Delay(200);

                try
                {
                    await serverHub.Publish(new MyMessage() { DeliverAtLeastOnce = true });
                }
                catch (NoSubscribersException)
                {
                    @event.Set();
                }
            });

            //await Task.Delay(1_000);

            await hub.ConnectServer(pipeName);
            await hub.SubscribeOnServer<MyMessage>(Handler);
            await Task.Delay(100);
            clientConnectedEvent.Set();
            @event.Wait(5000);

            // unsubscribing
            await hub.Unsubscribe<MyMessage>(Handler);
            Assert.IsTrue(@event.IsSet);
        }

        [TestMethod]
        public async Task ExceptionHandling()
        {
            var pipeName = Guid.NewGuid().ToString();
            var clientConnectedEvent = new ManualResetEventSlim();
            var notExpectedEvent = 0;
            var @event = new ManualResetEventSlim();

            // local hub creation
            var hub = new MetaPubSub();

            Task Handler(MyMessage x)
            {
                throw new InvalidOperationException("test");
            }

            // creating remote hub
            var t = Task.Run(async () =>
            {
                var serverHub = new MetaPubSub();
                serverHub.StartServer(pipeName);

                clientConnectedEvent.Wait();

                try
                {
                    await serverHub.Publish(new MyMessage2() { DeliverAtLeastOnce = true });
                    notExpectedEvent = 1;
                }
                catch (NoSubscribersException)
                {
                }
                catch (Exception)
                {
                    notExpectedEvent = 2;
                }

                try
                {
                    await serverHub.Publish(new MyMessage() { DeliverAtLeastOnce = true });
                    notExpectedEvent = 3;
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerException.GetType() != typeof(InvalidOperationException))
                        notExpectedEvent = 4;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    notExpectedEvent = 5;
                }

                @event.Set();
                //await Task.Delay(100_000);
            });

            await hub.ConnectServer(pipeName);
            await hub.SubscribeOnServer<MyMessage>(Handler);
            await Task.Delay(100);
            clientConnectedEvent.Set();
            @event.Wait();

            try
            {
                await hub.PublishOnServer(new MyMessage() { DeliverAtLeastOnce = true });
                notExpectedEvent = 6;
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException.GetType() != typeof(InvalidOperationException))
                    notExpectedEvent = 7;
            }
            catch (Exception)
            {
                notExpectedEvent = 8;
            }

            // unsubscribing
            await hub.Unsubscribe<MyMessage>(Handler);
            Console.WriteLine($"Not expected event: {notExpectedEvent}");
            Assert.IsTrue(@event.IsSet);
            Assert.IsTrue(notExpectedEvent == 0);
        }

        [TestMethod]
        public async Task DisconnectServer()
        {
            int loopCount = 100;
            int receivedCount = 0;

            Task Handler(PingReplay x)
            {
                receivedCount++;
                return Task.CompletedTask;
            }


            // server ------------------------
            var serverHub = CreateServerHub();
            serverHub.Subscribe<PingCommand>(OnPing);

            async Task OnPing(PingCommand ping)
            {
                await serverHub.Publish(new PingReplay() { Id = ping.Id });
            }
            // server ------------------------


            for (int i = 0; i < loopCount; i++)
            {
                // local hub creation
                var hub = new MetaPubSub();
                await hub.ConnectServer(serverHub.PipeName);
                await hub.SubscribeOnServer<PingReplay>(Handler);

                await hub.PublishOnServer(new PingCommand());

                await Task.Delay(50);
                await hub.Unsubscribe<PingReplay>(Handler);
                hub.DisconnectServer();
            }

            Assert.IsTrue(receivedCount == loopCount);
        }

        MetaPubSub CreateServerHub()
        {
            var pipeName = Guid.NewGuid().ToString();
            var serverHub = new MetaPubSub();
            serverHub.StartServer(pipeName);
            return serverHub;
        }
    }
}
