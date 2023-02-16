using Meta.Lib.Modules.Pipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;
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
        public async Task ClientConnectAndSend()
        {
            ILogger<PipeTests> logger = CreateLogger();
            string pipeName = NewPipeName();
            using AutoResetEvent receiveEvent = new(false);
            string sentMessage = "12345678";
            string receivedMessage = null;

            void Connection_MessageReceived(object sender, PipeConnectionMessageEventArgs e)
            {
                receivedMessage = Encoding.UTF8.GetString(e.Data);
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
            void Connection_MessageReceived(object sender, PipeConnectionMessageEventArgs e)
            {
                lock (receiveEvent)
                {
                    var receivedMessage = Encoding.UTF8.GetString(e.Data);
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

            void Client_MessageReceived(object sender, PipeConnectionMessageEventArgs e)
            {
                receivedMessage = Encoding.UTF8.GetString(e.Data);
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

            void Connection_MessageReceived(object sender, PipeConnectionMessageEventArgs e)
            {
                string receivedMessage = Encoding.UTF8.GetString(e.Data);
                receivedSum += Convert.ToInt32(receivedMessage);
                ++receivedCount;
            }

            void Connection_MessageReceived2(object sender, PipeConnectionMessageEventArgs e)
            {
                string receivedMessage = Encoding.UTF8.GetString(e.Data);
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
            void Connection_MessageReceived(object sender, PipeConnectionMessageEventArgs e)
            {
                string receivedMessage = Encoding.UTF8.GetString(e.Data);
                serverReceivedSum += Convert.ToInt32(receivedMessage);

                if (++serverReceivedCount == COUNT * 2)
                    allReceivedEvent1.Set();
            }

            // client side receive
            int clientReceivedCount = 0;
            long clientReceivedSum = 0;
            void Client_MessageReceived(object sender, PipeConnectionMessageEventArgs e)
            {
                string receivedMessage = Encoding.UTF8.GetString(e.Data);
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

            void Connection_MessageReceived(object sender, PipeConnectionMessageEventArgs e)
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
                if (!receiveEvent.WaitOne(1000))
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
