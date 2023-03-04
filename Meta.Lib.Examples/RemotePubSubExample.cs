using Meta.Lib.Examples.Shared;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSubPipe;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Meta.Lib.Examples
{
    public class RemotePubSubExample
    {
        internal async void RunAllExamples()
        {
            Console.WriteLine("BasicExample start -----------------");
            await BasicExample();
            Console.WriteLine("");
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

            // Client hub creation. There are can be several hubs connected to the same server.
            var clientHub = new MetaPubSub();
            using var pipeClient = new PubSubPipeClient(clientHub, "Meta");

            // Connecting to the remote server.
            await pipeClient.ConnectToServer();

            // Subscribing to MyMessage on the server and locally
            await pipeClient.SubscribeOnServer<MyMessage>();
            clientHub.Subscribe<MyMessage>(Handler);

            // The server publishes a message.
            await serverHub.Publish(new MyMessage());

            // Client hub publishes a message and it will be received locally without being sent to the server.
            await clientHub.Publish(new MyMessage());

            // Client hub sends a message to the server where it will be published and sent back.
            await pipeClient.PublishOnServer(new MyMessage());

            // All three messages should be received.
            Debug.Assert(count == 3);

            // Unsubscribing both on the server-side and locally.
            await pipeClient.UnsubscribeOnServer<MyMessage>();
            clientHub.Unsubscribe<MyMessage>(Handler);
        }
    }
}
