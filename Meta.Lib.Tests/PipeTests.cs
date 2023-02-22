using Meta.Lib.Examples.Shared;
using Meta.Lib.Modules.Pipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Tests
{
    [TestClass]
    public class PipeTests
    {
        static int PipeCount = 0;

        private static ILogger<PipeTests> CreateLogger()
        {
            IHost host = Host.CreateDefaultBuilder().Build();
            var factory = host.Services.GetService<ILoggerFactory>();
            var logger = factory.CreateLogger<PipeTests>();
            return logger;
        }

        private static string NewPipeName()
        {
            int no = Interlocked.Increment(ref PipeCount);
            return $"PS{no}";
        }


        [TestMethod]
        public async Task StateMachine()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();

            using var server = new MetaPipeServer(pipeName, logger);
            server.Start();

            using var client = new MetaPipeConnection(pipeName, logger);

            await client.Connect();
            client.Disconnect();

            await client.Connect();
            client.Disconnect();

            //await Task.Delay(1000);
            server.Stop();
        }

        [TestMethod]
        public async Task SendGeneric()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();
            using AutoResetEvent receiveEvent = new(false);
            var sentMessage = new MyMessage() { Message = "Hello", SomeId = 123, Version = new Version(1, 2, 3, 4) };
            MyMessage receivedMessage = null;

            void Connection_MessageReceived(object sender, PipeMessageEventArgs e)
            {
                receivedMessage = (MyMessage)e.Obj;
                receiveEvent.Set();
            }

            using var server = new MetaPipeServer(pipeName, logger);
            server.ClientConnected += (sender, e) =>
            {
                e.Connection.MessageReceived += Connection_MessageReceived;
            };
            server.Start();

            using var client = new MetaPipeConnection(pipeName, logger);
            await client.Connect();
            await client.Send(sentMessage, Guid.NewGuid());

            bool waitResult = receiveEvent.WaitOne(1000);
            Assert.IsTrue(waitResult);
            Assert.AreEqual(sentMessage.Message, receivedMessage.Message);
            Assert.AreEqual(sentMessage.SomeId, receivedMessage.SomeId);
            Assert.AreEqual(sentMessage.Version, receivedMessage.Version);
        }

        [TestMethod]
        public async Task SendAndWaitResponseAsObject()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();
            var sentMessage = new MyMessage() { Message = "Hello", SomeId = 123, Version = new Version(1, 2, 3, 4) };
            MyMessageResponse receivedMessage = null;

            async void Connection_MessageReceived(object sender, PipeMessageEventArgs e)
            {
                var connection = (MetaPipeConnection)sender;
                var request = (MyMessage)e.Obj;
                var response = new MyMessageResponse() { Message = request.Message, SomeId = request.SomeId, Version = request.Version };
                await connection.Send(response, e.CorrelationId);
            }

            using var server = new MetaPipeServer(pipeName, logger);
            server.ClientConnected += (sender, e) =>
            {
                e.Connection.MessageReceived += Connection_MessageReceived;
            };
            server.Start();

            using var client = new MetaPipeConnection(pipeName, logger);
            await client.Connect();
            receivedMessage = await client.SendAndWaitResponse<MyMessage, MyMessageResponse>(sentMessage, Guid.NewGuid());

            Assert.AreEqual(sentMessage.Message, receivedMessage.Message);
            Assert.AreEqual(sentMessage.SomeId, receivedMessage.SomeId);
            Assert.AreEqual(sentMessage.Version, receivedMessage.Version);
        }

        [TestMethod]
        public async Task SendAndWaitResponseAsArray()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();
            var sentMessage = new MyMessage() { Message = "Hello", SomeId = 123, Version = new Version(1, 2, 3, 4) };
            byte[] receivedMessage = null;

            async void Connection_MessageReceived(object sender, PipeMessageEventArgs e)
            {
                var connection = (MetaPipeConnection)sender;
                var request = (MyMessage)e.Obj;
                var response = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
                await connection.Send(response, e.CorrelationId);
            }

            using var server = new MetaPipeServer(pipeName, logger);
            server.ClientConnected += (sender, e) =>
            {
                e.Connection.MessageReceived += Connection_MessageReceived;
            };
            server.Start();

            using var client = new MetaPipeConnection(pipeName, logger);
            await client.Connect();
            receivedMessage = await client.SendAndWaitResponse<MyMessage, byte[]>(sentMessage, Guid.NewGuid());

            Assert.AreEqual(0x01, receivedMessage[0]);
            Assert.AreEqual(0x02, receivedMessage[1]);
            Assert.AreEqual(0x03, receivedMessage[2]);
            Assert.AreEqual(0x04, receivedMessage[3]);
            Assert.AreEqual(0x05, receivedMessage[4]);
        }

        [TestMethod]
        public async Task SendAndWaitResponseTimeout()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();
            var sentMessage = new MyMessage() { Message = "Hello", SomeId = 123, Version = new Version(1, 2, 3, 4) };

            using var server = new MetaPipeServer(pipeName, logger);
            server.Start();

            using var client = new MetaPipeConnection(pipeName, logger);
            await client.Connect();

            try
            {
                var receivedMessage = await client.SendAndWaitResponse<MyMessage, byte[]>(sentMessage, Guid.NewGuid(), 100);
                Assert.IsTrue(false);
            }
            catch (TimeoutException)
            {
            }
        }

        [TestMethod]
        public async Task SendAndWaitResponseWithError()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();
            var sentMessage = new MyMessage() { Message = "Hello", SomeId = 123, Version = new Version(1, 2, 3, 4) };
            byte[] receivedMessage = null;

            async void Connection_MessageReceived(object sender, PipeMessageEventArgs e)
            {
                var connection = (MetaPipeConnection)sender;
                var error = new ErrorDescription("some error message", 123);
                await connection.Send(error, e.CorrelationId);
            }

            using var server = new MetaPipeServer(pipeName, logger);
            server.ClientConnected += (sender, e) =>
            {
                e.Connection.MessageReceived += Connection_MessageReceived;
            };
            server.Start();

            using var client = new MetaPipeConnection(pipeName, logger);
            await client.Connect();

            try
            {
                receivedMessage = await client.SendAndWaitResponse<MyMessage, byte[]>(sentMessage, Guid.NewGuid());
                Assert.IsTrue(false);
            }
            catch (RemoteException ex)
            {
                Assert.AreEqual("some error message", ex.Error.Message);
                Assert.AreEqual(123, ex.Error.ErrorCode);
            }
        }

        [TestMethod]
        public async Task ClientConnectAndSend()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();
            using AutoResetEvent receiveEvent = new(false);
            string sentMessage = "12345678";
            string receivedMessage = null;

            void Connection_MessageReceived(object sender, PipeMessageEventArgs e)
            {
                receivedMessage = e.Str;
                receiveEvent.Set();
            }

            using var server = new MetaPipeServer(pipeName, logger);
            server.ClientConnected += (sender, e) =>
            {
                e.Connection.MessageReceived += Connection_MessageReceived;
            };

            server.Start();

            using var client = new MetaPipeConnection(pipeName, logger);
            await client.Connect();
            await client.Send(sentMessage);

            bool waitResult = receiveEvent.WaitOne(1000);
            Assert.IsTrue(waitResult);
            Assert.AreEqual(sentMessage, receivedMessage);

            server.Stop();
        }

        [TestMethod]
        public async Task InternalBufferResize()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();

            using var server = new MetaPipeServer(pipeName, logger);
            server.Start();

            using var client = new MetaPipeConnection(pipeName, logger);
            await client.Connect();

            long totalBytesSent = 0;
            long bufSize = 3;
            while (bufSize < 1 * 1024 * 1024)
            {
                var buf = new byte[bufSize];
                await client.Send(buf);
                totalBytesSent += bufSize;
                bufSize += 1234;
            }

            Assert.AreEqual(totalBytesSent, server.TotalBytesReceived);

            server.Stop();
        }

        [TestMethod]
        public async Task ConnectDisconnect()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();
            const int COUNT = 100;
            using AutoResetEvent receiveEvent = new(false);

            long receivedSum = 0;
            int receivedCount = 0;
            void Connection_MessageReceived(object sender, PipeMessageEventArgs e)
            {
                lock (receiveEvent)
                {
                    var receivedMessage = e.Str;
                    receivedSum += Convert.ToInt32(receivedMessage);

                    if (++receivedCount == COUNT)
                        receiveEvent.Set();
                }
            }

            int connectedClients = 0;
            using var server = new MetaPipeServer(pipeName, logger);
            server.ClientConnected += (sender, e) =>
            {
                connectedClients++;
                e.Connection.MessageReceived += Connection_MessageReceived;
            };

            server.Start();

            using var client = new MetaPipeConnection(pipeName, logger);

            long sentSum = 0;
            for (int i = 0; i < COUNT; i++)
            {
                await client.Connect();
                await client.Send(i.ToString());
                client.Disconnect();

                sentSum += i;
            }

            bool waitResult = receiveEvent.WaitOne(1000);
            Assert.IsTrue(waitResult);
            Assert.AreEqual(COUNT, receivedCount);
            Assert.AreEqual(COUNT, connectedClients);
            Assert.AreEqual(sentSum, receivedSum);

            server.Stop();
        }

        [TestMethod]
        public async Task ClientConnect_ServerSend()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();
            using AutoResetEvent receiveEvent = new(false);
            string sentMessage = "12345678";
            string receivedMessage = null;

            void Client_MessageReceived(object sender, PipeMessageEventArgs e)
            {
                receivedMessage = e.Str;
                receiveEvent.Set();
            }

            using var server = new MetaPipeServer(pipeName, logger);
            server.ClientConnected += async (sender, e) =>
            {
                await e.Connection.Send(sentMessage);
            };

            server.Start();

            using var client = new MetaPipeConnection(pipeName, logger);
            client.MessageReceived += Client_MessageReceived;
            await client.Connect();

            bool waitResult = receiveEvent.WaitOne(1000);
            Assert.IsTrue(waitResult);
            Assert.AreEqual(sentMessage, receivedMessage);

            server.Stop();
        }

        [TestMethod]
        public async Task SendMultiThread_2Subscribers()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();
            const int COUNT = 1000;
            using AutoResetEvent allReceivedEvent = new(false);

            int receivedCount = 0;
            long receivedSum = 0;

            int receivedCount2 = 0;
            long receivedSum2 = 0;

            void Connection_MessageReceived(object sender, PipeMessageEventArgs e)
            {
                string receivedMessage = e.Str;
                receivedSum += Convert.ToInt32(receivedMessage);
                ++receivedCount;
            }

            void Connection_MessageReceived2(object sender, PipeMessageEventArgs e)
            {
                string receivedMessage = e.Str;
                receivedSum2 += Convert.ToInt32(receivedMessage);
                if (++receivedCount2 == COUNT * 2)
                    allReceivedEvent.Set();
            }

            using var server = new MetaPipeServer(pipeName, logger);
            server.ClientConnected += (sender, e) =>
            {
                e.Connection.MessageReceived += Connection_MessageReceived;
                e.Connection.MessageReceived += Connection_MessageReceived2;
            };

            server.Start();

            using var client = new MetaPipeConnection(pipeName, logger);
            await client.Connect();
            long sentSum1 = 0;
            long sentSum2 = 0;

            var t1 = Task.Run(async () =>
            {
                for (int i = 0; i < COUNT; i++)
                {
                    sentSum1 += i;
                    await client.Send(i.ToString());
                }
            });

            var t2 = Task.Run(async () =>
            {
                for (int i = 0; i < COUNT; i++)
                {
                    sentSum2 += i;
                    await client.Send(i.ToString());
                }
            });

            await Task.WhenAll(t1, t2);

            bool waitResult = allReceivedEvent.WaitOne(1000);
            Assert.IsTrue(waitResult);
            Assert.AreEqual(COUNT * 2, receivedCount);
            Assert.AreEqual(COUNT * 2, receivedCount2);
            Assert.AreEqual(sentSum1 + sentSum2, receivedSum);
            Assert.AreEqual(sentSum1 + sentSum2, receivedSum2);

            server.Stop();
        }

        [TestMethod]
        public async Task SendMultiThread_BiDirection()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();
            const int COUNT = 1000;
            using AutoResetEvent allReceivedEvent1 = new(false);
            using AutoResetEvent allReceivedEvent2 = new(false);
            using AutoResetEvent clientConnectedEvent = new(false);
            long sentSum = 0;

            // server side receive
            int serverReceivedCount = 0;
            long serverReceivedSum = 0;
            void Connection_MessageReceived(object sender, PipeMessageEventArgs e)
            {
                string receivedMessage = e.Str;
                serverReceivedSum += Convert.ToInt32(receivedMessage);

                if (++serverReceivedCount == COUNT * 2)
                    allReceivedEvent1.Set();
            }

            // client side receive
            int clientReceivedCount = 0;
            long clientReceivedSum = 0;
            void Client_MessageReceived(object sender, PipeMessageEventArgs e)
            {
                string receivedMessage = e.Str;
                clientReceivedSum += Convert.ToInt32(receivedMessage);

                if (++clientReceivedCount == COUNT * 2)
                    allReceivedEvent2.Set();
            }

            // server initialization ---------------------------------
            using var server = new MetaPipeServer(pipeName, logger);

            Task sendToClientTask1 = null;
            Task sendToClientTask2 = null;
            server.ClientConnected += (sender, e) =>
            {
                e.Connection.MessageReceived += Connection_MessageReceived;

                sendToClientTask1 = Task.Run(async () =>
                {
                    for (int i = 0; i < COUNT; i++)
                    {
                        sentSum += i;
                        await e.Connection.Send(i.ToString());
                    }
                });

                sendToClientTask2 = Task.Run(async () =>
                {
                    for (int i = 0; i < COUNT; i++)
                    {
                        await e.Connection.Send(i.ToString());
                    }
                });

                clientConnectedEvent.Set();
            };

            server.Start();

            // client initialization -------------------------
            using var client = new MetaPipeConnection(pipeName, logger);
            client.MessageReceived += Client_MessageReceived;
            await client.Connect();

            var sendToServerTask1 = Task.Run(async () =>
            {
                for (int i = 0; i < COUNT; i++)
                {
                    await client.Send(i.ToString());
                }
            });

            var sendToServerTask2 = Task.Run(async () =>
            {
                for (int i = 0; i < COUNT; i++)
                {
                    await client.Send(i.ToString());
                }
            });


            // results ---------------------------------------
            clientConnectedEvent.WaitOne();
            await Task.WhenAll(sendToServerTask1, sendToServerTask2, sendToClientTask1, sendToClientTask2);

            bool waitResult = WaitHandle.WaitAll(new WaitHandle[] { allReceivedEvent1, allReceivedEvent2 }, 1000);

            Assert.IsTrue(waitResult);
            Assert.AreEqual(COUNT * 2, serverReceivedCount);
            Assert.AreEqual(sentSum * 2, serverReceivedSum);
            Assert.AreEqual(COUNT * 2, clientReceivedCount);
            Assert.AreEqual(sentSum * 2, clientReceivedSum);

            server.Stop();
        }

        [TestMethod]
        public async Task ServerStartStop()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();
            const int COUNT = 100;
            int connectedClients = 0;
            int connectedClients2 = 0;
            using AutoResetEvent receiveEvent = new(false);

            void Connection_MessageReceived(object sender, PipeMessageEventArgs e)
            {
                receiveEvent.Set();
            }

            void Connection_Connected(object sender, EventArgs e)
            {
                connectedClients2++;
            }

            using var server = new MetaPipeServer(pipeName, logger);
            server.ClientConnected += (sender, e) =>
            {
                connectedClients++;
                e.Connection.MessageReceived += Connection_MessageReceived;
            };

            for (int i = 0; i < COUNT; i++)
            {
                server.Start();

                using var client = new MetaPipeConnection(pipeName, logger);
                client.Connected += Connection_Connected;

                await client.Connect();

                await client.Send(i.ToString());
                if (!receiveEvent.WaitOne(5000))
                    Assert.Fail("send failed");

                server.Stop();
            }

            Assert.AreEqual(COUNT, connectedClients, "connected clients");
            Assert.AreEqual(COUNT, connectedClients2, "connection connected");
        }

        [TestMethod]
        public async Task ClientReconnect()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();
            const int COUNT = 100;
            int connectedClients = 0;
            int connectedClients2 = 0;
            using AutoResetEvent connectedEvent = new(false);
            using AutoResetEvent connectedEvent2 = new(false);

            void Client_Connected(object sender, EventArgs e)
            {
                connectedClients++;
                connectedEvent.Set();
            }

            void Server_ClientConnected(object sender, PipeConnectionEventArgs e)
            {
                connectedClients2++;
                connectedEvent2.Set();
            }

            using var server = new MetaPipeServer(pipeName, logger);
            server.ClientConnected += Server_ClientConnected;
            server.Start();

            using var client = new MetaPipeConnection(pipeName, logger);
            client.Connected += Client_Connected;

            await client.Connect();
            if (!connectedEvent.WaitOne(1000))
                Assert.Fail("connect failed");
            if (!connectedEvent2.WaitOne(1000))
                Assert.Fail("connect failed 2");

            for (int i = 0; i < COUNT; i++)
            {
                server.Stop();
                server.Start();

                if (!connectedEvent.WaitOne(5000))
                    Assert.Fail("reconnect failed");

                if (!connectedEvent2.WaitOne(5000))
                    Assert.Fail("reconnect failed 2");
            }

            Assert.AreEqual(COUNT + 1, connectedClients, "connected clients");
            Assert.AreEqual(COUNT + 1, connectedClients2, "connected clients 2");

            server.Stop();
        }


    }
}
