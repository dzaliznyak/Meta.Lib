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
        MetaPubSub CreateServerHub(string pipeName = null)
        {
            pipeName ??= Guid.NewGuid().ToString();
            var serverHub = new MetaPubSub();
            serverHub.StartServer(pipeName);
            return serverHub;
        }

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
            await hub.ConnectToServer(pipeName);
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
            await hub.ConnectToServer(pipeName);
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
                await serverHub.Publish(new MyMessage() { DeliverAtLeastOnce = true, WaitForSubscriberTimeout = 20_000 });
            });

            await Task.Delay(1_000);

            // local hub creation
            var hub = new MetaPubSub();
            await hub.ConnectToServer(pipeName);
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
                await hub.ConnectToServer(pipeName);
                await hub.PublishOnServer(new MyMessage(text));
            });

            // creating server
            var serverHub = new MetaPubSub();
            serverHub.StartServer(pipeName);
            serverHub.Subscribe<MyMessage>(Handler);

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

            async Task Handler(MyMessage x)
            {
                await hub.Unsubscribe<MyMessage>(Handler);
            }

            // creating remote hub
            var t = Task.Run(async () =>
            {
                var serverHub = new MetaPubSub();
                serverHub.StartServer(pipeName);

                clientConnectedEvent.Wait();
                // publishing a message at the remote hub
                await serverHub.Publish(new MyMessage() { DeliverAtLeastOnce = true });

                try
                {
                    await serverHub.Publish(new MyMessage() { DeliverAtLeastOnce = true });
                }
                catch (NoSubscribersException)
                {
                    @event.Set();
                }
            });

            await hub.ConnectToServer(pipeName);
            await hub.SubscribeOnServer<MyMessage>(Handler);
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

            static Task Handler(MyMessage x)
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
            });

            await hub.ConnectToServer(pipeName);
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
                await hub.ConnectToServer(serverHub.PipeName);
                await hub.SubscribeOnServer<PingReplay>(Handler);

                await hub.PublishOnServer(new PingCommand());

                await hub.Unsubscribe<PingReplay>(Handler);
                await hub.DisconnectFromServer();
            }

            Assert.IsTrue(receivedCount == loopCount);
        }

        [TestMethod]
        public async Task LostConnection()
        {
            var pipeName = Guid.NewGuid().ToString();
            int receivedCount = 0;
            int publishedCount = 0;
            string lastReceivedId = "";

            Task Handler(PingCommand x)
            {
                lastReceivedId = x.Id;
                receivedCount++;
                return Task.CompletedTask;
            }

            // server ------------------------
            var serverHub = CreateServerHub(pipeName);

            // local hub creation
            var hub = new MetaPubSub();
            await hub.ConnectToServer(pipeName);
            await hub.SubscribeOnServer<PingCommand>(Handler);

            // break the connection after 1 sec
            var t = Task.Run(async () =>
            {
                await Task.Delay(1_000);
                serverHub.StopServer();
            });


            int i = 0;
            try
            {
                for (i = 0; i < 1_000_000; i++)
                {
                    await hub.PublishOnServer(new PingCommand() { Id = i.ToString() });
                    publishedCount++;
                }
            }
            catch (Exception)
            {
                // in the case when the message was received by the client and counted
                // but the confirmation was failed to deliver back to the server
                if (i.ToString() == lastReceivedId)
                {
                    Trace.WriteLine($"decrement");
                    receivedCount--;
                }
            }

            Trace.WriteLine($"receivedCount - {receivedCount}, publishedCount - {publishedCount}");

            // start the server again
            serverHub.StartServer(pipeName);

            // wait for connection
            while (!hub.IsConnectedToServer)
                await Task.Delay(100);

            Trace.WriteLine($"receivedCount - {receivedCount}, publishedCount - {publishedCount}");

            for (i = 0; i < 100; i++)
            {
                await hub.PublishOnServer(new PingCommand());
                publishedCount++;
            }

            Trace.WriteLine($"receivedCount - {receivedCount}, publishedCount - {publishedCount}");
            Assert.IsTrue(receivedCount - publishedCount <= 1);
        }

        [TestMethod]
        public async Task When()
        {
            static Task Handler(PingCommand x)
            {
                return Task.CompletedTask;
            }

            // server ------------------------
            var serverHub = CreateServerHub();

            // local hub creation
            var hub = new MetaPubSub();
            await hub.ConnectToServer(serverHub.PipeName);
            await hub.SubscribeOnServer<PingCommand>(Handler);

            var t = Task.Run(async () =>
            {
                await Task.Delay(200);
                await hub.PublishOnServer(new PingCommand() { Id = "321" });
                await hub.PublishOnServer(new PingCommand() { Id = "123" });
            });

            var ping = await hub.When<PingCommand>(1000, x => x.Id == "123");
            Assert.IsTrue(ping.Id == "123");
        }

        [TestMethod]
        public async Task Process()
        {
            // server ------------------------
            var serverHub = CreateServerHub();

            async Task Handler(PingCommand x)
            {
                await serverHub.Publish(new PingReplay() { Id = x.Id });
            }
            serverHub.Subscribe<PingCommand>(Handler);

            // local hub creation
            var hub = new MetaPubSub();
            await hub.ConnectToServer(serverHub.PipeName);

            var ping = new PingCommand() { Id = "123", ResponseTimeout = 1000 };
            var pingReply = await hub.ProcessOnServer<PingReplay>(ping);

            Assert.IsTrue(pingReply.Id == "123");
        }

        [TestMethod]
        public async Task DelayedSubscribe()
        {
            var pipeName = Guid.NewGuid().ToString();
            MetaPubSub serverHub = null;

            bool received = false;
            Task Handler(PingCommand x)
            {
                received = true;
                return Task.CompletedTask;
            }

            // local hub creation
            var hub = new MetaPubSub();
            await hub.TryConnectToServer(pipeName, 100, 500);
            await hub.TrySubscribeOnServer<PingCommand>(Handler);

            // server ------------------------
            serverHub = CreateServerHub(pipeName);

            await Task.Delay(1000);

            await hub.PublishOnServer(new PingCommand());

            Assert.IsTrue(received);
        }

        [TestMethod]
        public async Task ConnectionCancellation()
        {
            var pipeName = Guid.NewGuid().ToString();

            bool received = false;
            Task Handler(PingCommand x)
            {
                received = true;
                return Task.CompletedTask;
            }

            // local hub creation
            var hub = new MetaPubSub();
            await hub.TryConnectToServer(pipeName, 1_000, 100);
            await hub.TrySubscribeOnServer<PingCommand>(Handler);
            await hub.DisconnectFromServer();

            // server ------------------------
            var serverHub = CreateServerHub(pipeName);
            await Task.Delay(1000);
            await serverHub.Publish(new PingCommand() { DeliverAtLeastOnce = false });

            Assert.IsFalse(received);
        }

        [TestMethod]
        public async Task Connect()
        {
            var pipeName = Guid.NewGuid().ToString();

            // server ------------------------
            CreateServerHub(pipeName);

            // local hub creation
            var hub = new MetaPubSub();
            try
            {
                await hub.DisconnectFromServer();
                Assert.IsTrue(false);
            }
            catch (InvalidOperationException ex)
            {
                Trace.WriteLine(ex.Message);
            }

            await hub.ConnectToServer(pipeName, 1_000);

            try
            {
                await hub.TryConnectToServer(pipeName, 1_000);
                Assert.IsTrue(false);
            }
            catch (InvalidOperationException ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        [TestMethod]
        public async Task TryConnect()
        {
            var pipeName = Guid.NewGuid().ToString();

            var hub = new MetaPubSub();
            var res = await hub.TryConnectToServer(pipeName, 10, 500);
            Assert.IsFalse(res);

            // server ------------------------
            CreateServerHub(pipeName);
            await Task.Delay(1000);
            Assert.IsTrue(hub.IsConnectedToServer);
        }

        [TestMethod]
        public async Task Subscribe()
        {
            var pipeName = Guid.NewGuid().ToString();
            bool received = false;
            Task Handler(PingCommand x)
            {
                received = true;
                return Task.CompletedTask;
            }

            var hub = new MetaPubSub();

            try
            {
                await hub.SubscribeOnServer<PingCommand>(Handler);
                Assert.IsTrue(false);
            }
            catch (NotConnectedToServerException ex)
            {
                Trace.WriteLine(ex.Message);
            }

            // server ------------------------
            var serverHub = CreateServerHub(pipeName);

            await hub.ConnectToServer(pipeName);
            await hub.SubscribeOnServer<PingCommand>(Handler);

            await serverHub.Publish(new PingCommand());
            Assert.IsTrue(received);
        }


        [TestMethod]
        public async Task TrySubscribe()
        {
            var pipeName = Guid.NewGuid().ToString();
            bool received = false;
            Task Handler(PingCommand x)
            {
                received = true;
                return Task.CompletedTask;
            }

            var hub = new MetaPubSub();
            var res = await hub.TrySubscribeOnServer<PingCommand>(Handler);
            Assert.IsFalse(res);

            // server ------------------------
            var serverHub = CreateServerHub(pipeName);

            await hub.ConnectToServer(pipeName);

            await serverHub.Publish(new PingCommand());
            Assert.IsTrue(received);
        }

        [TestMethod]
        public async Task TwoClients()
        {
            var pipeName = Guid.NewGuid().ToString();
            // server ------------------------
            var serverHub = CreateServerHub(pipeName);


            int receivedCount = 0;
            Task Handler(PingCommand x)
            {
                receivedCount++;
                return Task.CompletedTask;
            }

            // hub 1
            var hub1 = new MetaPubSub();
            await hub1.ConnectToServer(pipeName);
            await hub1.SubscribeOnServer<PingCommand>(Handler);

            await serverHub.Publish(new PingCommand());
            Assert.IsTrue(receivedCount == 1);

            // hub 2
            var hub2 = new MetaPubSub();
            await hub2.ConnectToServer(pipeName);
            await hub2.SubscribeOnServer<PingCommand>(Handler);


            await serverHub.Publish(new PingCommand());
            Assert.IsTrue(receivedCount == 3);

            await hub1.Unsubscribe<PingCommand>(Handler);

            await serverHub.Publish(new PingCommand());
            Assert.IsTrue(receivedCount == 4);

            await hub2.Unsubscribe<PingCommand>(Handler);

            await serverHub.Publish(new PingCommand() { DeliverAtLeastOnce = false });
            Assert.IsTrue(receivedCount == 4);
        }

        [TestMethod]
        public async Task TwoHandlers()
        {
            var pipeName = Guid.NewGuid().ToString();
            // server ------------------------
            var serverHub = CreateServerHub(pipeName);

            int receivedCount = 0;
            Task Handler1(PingCommand x)
            {
                receivedCount++;
                return Task.CompletedTask;
            }

            Task Handler2(PingCommand x)
            {
                receivedCount++;
                return Task.CompletedTask;
            }

            // client hub
            var hub = new MetaPubSub();
            await hub.ConnectToServer(pipeName);
            await hub.SubscribeOnServer<PingCommand>(Handler1);
            await hub.SubscribeOnServer<PingCommand>(Handler2);


            await serverHub.Publish(new PingCommand());
            Assert.IsTrue(receivedCount == 2);

            // unsubscribe first handler
            await hub.Unsubscribe<PingCommand>(Handler1);

            await serverHub.Publish(new PingCommand() { DeliverAtLeastOnce = false });
            Assert.IsTrue(receivedCount == 3);

            // unsubscribe second handler
            await hub.Unsubscribe<PingCommand>(Handler2);

            await serverHub.Publish(new PingCommand() { DeliverAtLeastOnce = false });
            Assert.IsTrue(receivedCount == 3);
        }

        [TestMethod]
        public async Task Predicate()
        {
            var pipeName = Guid.NewGuid().ToString();
            // server ------------------------
            var serverHub = CreateServerHub(pipeName);

            int receivedCount = 0;
            Task Handler1(MyMessage x)
            {
                receivedCount++;
                return Task.CompletedTask;
            }

            static bool Predicate(MyMessage message)
            {
                return message.Version > new Version(1, 0);
            }

            Task Handler2(MyMessage x)
            {
                receivedCount++;
                return Task.CompletedTask;
            }

            // client hub
            var hub = new MetaPubSub();
            await hub.ConnectToServer(pipeName);
            await hub.SubscribeOnServer<MyMessage>(Handler1, Predicate);
            await hub.SubscribeOnServer<MyMessage>(Handler2);


            var message = new MyMessage { Version = new Version(1, 0) };
            await serverHub.Publish(message);
            Assert.IsTrue(receivedCount == 1);


            message = new MyMessage { Version = new Version(1, 1) };
            await serverHub.Publish(message);
            Assert.IsTrue(receivedCount == 3);
        }

        [TestMethod]
        public async Task ProcessWithPredicate()
        {
            // server ------------------------
            var serverHub = CreateServerHub();
            serverHub.Subscribe<MyMessage>(Handler);

            async Task Handler(MyMessage x)
            {
                await serverHub.Publish(new MyMessageReplay() { SomeId = x.SomeId, Version = x.Version });
            }

            static bool Predicate(MyMessageReplay message)
            {
                return message.SomeId == 456;
            }

            // local hub creation
            var hub = new MetaPubSub();
            await hub.ConnectToServer(serverHub.PipeName);

            try
            {
                await hub.ProcessOnServer<MyMessageReplay>(
                    new MyMessage() { SomeId = 123, ResponseTimeout = 1_000 },
                    Predicate);

                // prev call must throw TimeoutException
                Assert.IsTrue(false);
            }
            catch (TimeoutException)
            {
            }

            var replay = await hub.ProcessOnServer<MyMessageReplay>(
                    new MyMessage() { SomeId = 456, ResponseTimeout = 1_000 },
                    Predicate);

            Assert.IsTrue(replay.SomeId == 456);
        }

        [TestMethod]
        public async Task PublishOnServer()
        {
            // server ------------------------
            var serverHub = CreateServerHub();

            static Task Handler(PingCommand x)
            {
                return Task.CompletedTask;
            }

            // local hub creation
            var hub = new MetaPubSub();

            try
            {
                await hub.PublishOnServer(new PingCommand() { Id = "123" });
                Assert.IsTrue(false);
            }
            catch (NotConnectedToServerException)
            {
            }

            await hub.ConnectToServer(serverHub.PipeName);

            try
            {
                await hub.PublishOnServer(new PingCommand() { DeliverAtLeastOnce = true, WaitForSubscriberTimeout = 0 });
            }
            catch (NoSubscribersException)
            {
            }

            try
            {
                await hub.PublishOnServer(new PingCommand() { DeliverAtLeastOnce = true, WaitForSubscriberTimeout = 100 });
            }
            catch (TimeoutException)
            {
            }

            serverHub.Subscribe<PingCommand>(Handler);

            await hub.PublishOnServer(new PingCommand() { DeliverAtLeastOnce = true, WaitForSubscriberTimeout = 100 });
        }


        [TestMethod]
        public async Task ConnectedDisconnectedEvents()
        {
            // server ------------------------
            var serverHub = CreateServerHub();

            serverHub.Subscribe<RemoteClientConnectedEvent>(ConnectedHandler);
            serverHub.Subscribe<RemoteClientDisconnectedEvent>(DisconnectedHandler);

            bool hasBeenConnected = false;
            Task ConnectedHandler(RemoteClientConnectedEvent x)
            {
                hasBeenConnected = true;
                return Task.CompletedTask;
            }

            bool hasBeenDisconnected = false;
            Task DisconnectedHandler(RemoteClientDisconnectedEvent x)
            {
                hasBeenDisconnected = true;
                return Task.CompletedTask;
            }

            bool hasBeenConnectedToServer = false;
            Task ConnectedToServerHandler(ConnectedToServerEvent x)
            {
                hasBeenConnectedToServer = true;
                return Task.CompletedTask;
            }

            bool hasBeenDisconnectedFromServer = false;
            Task DisconnectedFromServerHandler(DisconnectedFromServerEvent x)
            {
                hasBeenDisconnectedFromServer = true;
                return Task.CompletedTask;
            }

            // local hub creation
            var hub = new MetaPubSub();

            hub.Subscribe<ConnectedToServerEvent>(ConnectedToServerHandler);
            hub.Subscribe<DisconnectedFromServerEvent>(DisconnectedFromServerHandler);

            await hub.ConnectToServer(serverHub.PipeName);
            await hub.DisconnectFromServer();

            await Task.Delay(300);

            Assert.IsTrue(hasBeenConnected && hasBeenDisconnected &&
                          hasBeenConnectedToServer && hasBeenDisconnectedFromServer);
        }

    }
}
