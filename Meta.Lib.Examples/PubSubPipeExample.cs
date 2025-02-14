﻿using Meta.Lib.Examples.Shared;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSubPipe;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Meta.Lib.Examples
{
    public class PubSubPipeExample
    {
        internal async void RunAllExamples()
        {
            Console.WriteLine("BasicExample start -----------------");
            await BasicExample();
            Console.WriteLine("BasicExample end -----------------");
            Console.WriteLine("Press enter key to exit");
        }

        async Task BasicExample()
        {
            int count = 0;
            Task Handler(MyMessage x)
            {
                count++;
                return Task.CompletedTask;
            }

            // Creating the server hub.
            // The server and the client hubs should be created in separate processes, 
            // this example is for demo only.
            var serverHub = new MetaPubSub();
            using var pipeServer = new PubSubPipeServer("Meta", serverHub);

            // Starting the hub as a server named 'Meta'.
            pipeServer.StartServer();
            Console.WriteLine("Server started");

            // Client hub creation. There are can be several hubs connected to the same server.
            var clientHub = new MetaPubSub();
            using var pipeClient = new PubSubPipeClient("Meta", clientHub);

            // Connecting to the remote server.
            await pipeClient.ConnectToServer();
            Console.WriteLine("Client connected");


            // Subscribing to MyMessage on the server and locally
            await pipeClient.SubscribeOnServer<MyMessage>();
            clientHub.Subscribe<MyMessage>(Handler);
            Console.WriteLine("Subscribed");

            // The server publishes a message.
            await serverHub.Publish(new MyMessage());
            Console.WriteLine("Published message 1");

            // Client hub publishes a message and it will be received locally without being sent to the server.
            await clientHub.Publish(new MyMessage());
            Console.WriteLine("Published message 2");

            // Client hub sends a message to the server where it will be published and sent back.
            await pipeClient.PublishOnServer(new MyMessage());
            Console.WriteLine("Published message 3");

            // All three messages should be received.
            Debug.Assert(count == 3);
            Console.WriteLine("All messages received");

            // Unsubscribing both on the server-side and locally.
            await pipeClient.UnsubscribeOnServer<MyMessage>();
            clientHub.Unsubscribe<MyMessage>(Handler);
            Console.WriteLine("Unsubscribed");

            pipeServer.StopServer();
            Console.WriteLine("Server stopped");
        }
    }
}
