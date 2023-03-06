using Meta.Lib.Examples.Shared;
using Meta.Lib.Exceptions;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSubPipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Tests
{
    internal class HostHelper : IDisposable
    {
        static int PipeCount = 0;

        readonly IHost _host;
        readonly ILogger<PubSubPipeTests> _logger;

        MetaPubSub _serverPubSub;
        MetaPubSub _clientPubSub;
        PubSubPipeServer _pubSubPipeServer;
        PubSubPipeClient _pubSubPipeClient;

        public string PipeName { get; private set; }
        public MetaPubSub ServerPubSub => _serverPubSub;
        public MetaPubSub ClientPubSub => _clientPubSub;


        public HostHelper()
        {
            PipeName = NewPipeName();

            _host = Host.CreateDefaultBuilder().Build();
            using var factory = _host.Services.GetService<ILoggerFactory>();
            _logger = factory.CreateLogger<PubSubPipeTests>();
        }

        static string NewPipeName()
        {
            int no = Interlocked.Increment(ref PipeCount);
            return $"PS{no}";
        }

        public void Dispose()
        {
            _host.Dispose();
            _pubSubPipeServer?.Dispose();
            _pubSubPipeClient?.Dispose();
        }

        internal IPubSubPipeServer CreatePubSubPipeServer()
        {
            _serverPubSub = new MetaPubSub(_logger);
            _pubSubPipeServer = new PubSubPipeServer(PipeName, _serverPubSub, _logger);
            _pubSubPipeServer.StartServer();
            return _pubSubPipeServer;
        }

        internal IPubSubPipeClient CreatePubSubPipeClient(int connectTimeout = 1000)
        {
            _clientPubSub = new MetaPubSub(_logger);
            _pubSubPipeClient = new PubSubPipeClient(PipeName, _clientPubSub, _logger, connectTimeout);
            return _pubSubPipeClient;
        }
    }

    [TestClass]
    public class PubSubPipeTests
    {
        [TestMethod]
        public async Task PubSubPipeClient_ConnectToServer_EstablishesConnection()
        {
            using var host = new HostHelper();

            var pipeServer = host.CreatePubSubPipeServer();

            var pipeClient = host.CreatePubSubPipeClient();
            await pipeClient.ConnectToServer();

            Assert.IsTrue(pipeClient.IsConnected);
        }


        [TestMethod]
        public async Task PubSubPipeClient_TryConnectToServer_ConnectsAfterServerStarted()
        {
            using var host = new HostHelper();

            var pipeClient = host.CreatePubSubPipeClient(connectTimeout: 100);
            bool connectResult = await pipeClient.TryConnectToServer();
            Assert.IsFalse(connectResult);

            var pipeServer = host.CreatePubSubPipeServer();

            await Task.Delay(100);

            Assert.IsTrue(pipeClient.IsConnected);
        }

        [TestMethod]
        public async Task PubSubPipeServer_Publish_ClientReceivesMessage()
        {
            MyMessage sentMessage = new() { Message = "test" };
            MyMessage receivedMessage = null;

            Task handler(MyMessage arg)
            {
                receivedMessage = arg;
                return Task.CompletedTask;
            }

            using var host = new HostHelper();

            var server = host.CreatePubSubPipeServer();

            var client = host.CreatePubSubPipeClient();
            await client.ConnectToServer();
            await client.SubscribeOnServer<MyMessage>();

            host.ClientPubSub.Subscribe<MyMessage>(handler);

            await host.ServerPubSub.Publish(sentMessage, new PubSubOptions() { DeliverAtLeastOnce = true });

            Assert.AreEqual(sentMessage.Message, receivedMessage.Message);
        }


        [TestMethod]
        public async Task PubSubPipeClient_UnsubscribeOnServer_ClientNotReceivesMessage()
        {
            MyMessage sentMessage = new() { Message = "test" };
            MyMessage receivedMessage = null;
            Exception exception = null;

            Task handler(MyMessage arg)
            {
                receivedMessage = arg;
                return Task.CompletedTask;
            }

            using var host = new HostHelper();

            var server = host.CreatePubSubPipeServer();

            var client = host.CreatePubSubPipeClient();
            await client.ConnectToServer();
            await client.SubscribeOnServer<MyMessage>();
            await client.UnsubscribeOnServer<MyMessage>();

            host.ClientPubSub.Subscribe<MyMessage>(handler);

            try
            {
                await host.ServerPubSub.Publish(sentMessage, new PubSubOptions() { DeliverAtLeastOnce = true });
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsTrue(exception is NoSubscribersException);
        }

        [TestMethod]
        public async Task PubSubPipeClient_Publish_ServerReceivesMessage()
        {
            MyMessage sentMessage = new() { Message = "test" };
            MyMessage receivedMessage = null;

            Task handler(MyMessage arg)
            {
                receivedMessage = arg;
                return Task.CompletedTask;
            }

            using var host = new HostHelper();

            var client = host.CreatePubSubPipeClient();
            var server = host.CreatePubSubPipeServer();

            host.ServerPubSub.Subscribe<MyMessage>(handler);

            await client.ConnectToServer();
            await client.PublishOnServer(sentMessage);

            Assert.AreEqual(sentMessage.Message, receivedMessage.Message);
        }

        [TestMethod]
        public async Task PubSubPipeServer_PublishWhenNoSubscribers_ReceivesNoSubscriberException()
        {
            MyMessage sentMessage = new() { Message = "test" };
            Exception exception = null;

            using var host = new HostHelper();

            var client = host.CreatePubSubPipeClient();
            var server = host.CreatePubSubPipeServer();

            await client.ConnectToServer();
            //await client.SubscribeOnServer<MyMessage>(handler);

            try
            {
                await host.ServerPubSub.Publish(sentMessage, new PubSubOptions() { DeliverAtLeastOnce = true });
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsTrue(exception is NoSubscribersException);
        }

        [TestMethod]
        public async Task PubSubPipeServer_ClientHandlerThrowsException_ReceivesException()
        {
            MyMessage sentMessage = new() { Message = "test" };
            Exception exception = null;

            //Task handler(MyMessage arg)
            //{
            //    throw new Exception("some error");
            //}

            using var host = new HostHelper();
            var server = host.CreatePubSubPipeServer();

            var client = host.CreatePubSubPipeClient();
            await client.ConnectToServer();
            await client.SubscribeOnServer<MyMessage>();

            host.ClientPubSub.Subscribe<MyMessage>((m) => throw new Exception("some error"));

            try
            {
                await host.ServerPubSub.Publish(sentMessage);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsTrue(exception?.Message.Contains("some error") ?? false);
        }


        [TestMethod]
        public async Task Client_DisconnectPipe_ServerUnsubscribesAllMessages()
        {
            MyMessage sentMessage = new() { Message = "test" };
            MyMessage receivedMessage = null;
            Exception exception = null;

            Task handler(MyMessage arg)
            {
                receivedMessage = arg;
                return Task.CompletedTask;
            }

            using var host = new HostHelper();

            var server = host.CreatePubSubPipeServer();

            var client = host.CreatePubSubPipeClient();
            await client.ConnectToServer();
            await client.SubscribeOnServer<MyMessage>();

            host.ClientPubSub.Subscribe<MyMessage>(handler);

            await host.ServerPubSub.Publish(sentMessage, new PubSubOptions() { DeliverAtLeastOnce = true });
            Assert.AreEqual(sentMessage.Message, receivedMessage.Message);

            client.DisconnectFromServer();
            await Task.Delay(100);

            try
            {
                await host.ServerPubSub.Publish(sentMessage, new PubSubOptions() { DeliverAtLeastOnce = true });
            }
            catch (NoSubscribersException ex)
            {
                exception = ex;
            }
            Assert.IsTrue(exception is NoSubscribersException);
        }

        [TestMethod]
        public async Task Client_ProcessOnServer_ReceivesResponseWithoutSubscribing()
        {
            using var host = new HostHelper();
            var server = host.CreatePubSubPipeServer();
            var client = host.CreatePubSubPipeClient();
            await client.ConnectToServer();

            async Task Handler(MyMessage x)
            {
                await host.ServerPubSub.Publish(new MyMessageResponse() { SomeId = x.SomeId });
            }
            host.ServerPubSub.Subscribe<MyMessage>(Handler);


            var message = new MyMessage() { SomeId = 123 };

            var resp = await client.ProcessOnServer<MyMessage, MyMessageResponse>(message, responseTimeoutMs: 1000);

            Assert.AreEqual(message.SomeId, resp?.SomeId);
        }

        [TestMethod]
        public async Task Client_PublishOnServerWithoutHandler_ReceivesException()
        {
            Exception exception = null;
            using var host = new HostHelper();
            var server = host.CreatePubSubPipeServer();
            var client = host.CreatePubSubPipeClient();
            await client.ConnectToServer();

            var message = new MyMessage() { SomeId = 123 };

            try
            {
                await client.PublishOnServer(message, new PubSubOptions() { DeliverAtLeastOnce = true });
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsTrue(exception is NoSubscribersException);
        }

        [TestMethod]
        public async Task Client_ProcessOnServerWithoutHandler_ReceivesException()
        {
            Exception exception = null;
            using var host = new HostHelper();
            var server = host.CreatePubSubPipeServer();
            var client = host.CreatePubSubPipeClient();
            await client.ConnectToServer();

            var message = new MyMessage() { SomeId = 123 };

            try
            {
                var resp = await client.ProcessOnServer<MyMessage, MyMessageResponse>(message, responseTimeoutMs: 100);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsTrue(exception is TimeoutException);
        }


        [TestMethod]
        public async Task Client_TrySubscribe_ClientReceivesMessageAfterConnection()
        {
            MyMessage sentMessage = new() { Message = "test" };
            MyMessage receivedMessage = null;

            Task handler(MyMessage arg)
            {
                receivedMessage = arg;
                return Task.CompletedTask;
            }

            using var host = new HostHelper();

            // client
            var client = host.CreatePubSubPipeClient();
            bool subscribeResult = await client.TrySubscribeOnServer<MyMessage>();
            Assert.IsFalse(subscribeResult);

            host.ClientPubSub.Subscribe<MyMessage>(handler);

            // server
            var server = host.CreatePubSubPipeServer();

            // connection
            await client.ConnectToServer();

            client.WaitForReconnect(5000);

            await host.ServerPubSub.Publish(sentMessage, new PubSubOptions() { DeliverAtLeastOnce = true });

            Assert.AreEqual(sentMessage.Message, receivedMessage.Message);
        }


        [TestMethod]
        public async Task Server_Stop_ClientReceivesMessageAfterReConnection()
        {
            MyMessage sentMessage = new() { Message = "test" };
            MyMessage receivedMessage = null;

            Task handler(MyMessage arg)
            {
                receivedMessage = arg;
                return Task.CompletedTask;
            }

            using var host = new HostHelper();

            // server
            var server = host.CreatePubSubPipeServer();

            // client
            var client = host.CreatePubSubPipeClient();
            host.ClientPubSub.Subscribe<MyMessage>(handler);
            await client.ConnectToServer();
            bool subscribeResult = await client.TrySubscribeOnServer<MyMessage>();
            Assert.IsTrue(subscribeResult);

            await host.ServerPubSub.Publish(sentMessage, new PubSubOptions() { DeliverAtLeastOnce = true });
            Assert.AreEqual(sentMessage.Message, receivedMessage.Message);

            server.StopServer();
            server.StartServer();

            client.WaitForReconnect(5000);
            client.WaitForReconnect(5000);

            sentMessage.Message = "test2";
            await host.ServerPubSub.Publish(sentMessage, new PubSubOptions() { DeliverAtLeastOnce = true });

            Assert.AreEqual(sentMessage.Message, receivedMessage.Message);
        }

        [TestMethod]
        public async Task Client_Unsubscribe_SecondHandlerContinueToReceiveMessages()
        {
            int receivedCount = 0;
            Task Handler1(MyMessage x)
            {
                receivedCount++;
                return Task.CompletedTask;
            }

            Task Handler2(MyMessage x)
            {
                receivedCount++;
                return Task.CompletedTask;
            }
            
            using var host = new HostHelper();
            var server = host.CreatePubSubPipeServer();
            var client = host.CreatePubSubPipeClient();

            // client hub
            await client.ConnectToServer();
            await client.SubscribeOnServer<MyMessage>();
            host.ClientPubSub.Subscribe<MyMessage>(Handler1);
            host.ClientPubSub.Subscribe<MyMessage>(Handler2);


            await host.ServerPubSub.Publish(new MyMessage());
            Assert.IsTrue(receivedCount == 2);

            // unsubscribe first handler
            host.ClientPubSub.Unsubscribe<MyMessage>(Handler1);

            await host.ServerPubSub.Publish(new MyMessage());
            Assert.IsTrue(receivedCount == 3);

            // unsubscribe second handler
            host.ClientPubSub.Unsubscribe<MyMessage>(Handler2);

            await host.ServerPubSub.Publish(new MyMessage());
            Assert.IsTrue(receivedCount == 3);
        }


    }
}
