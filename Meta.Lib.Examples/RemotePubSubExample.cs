using Meta.Lib.Examples.Shared;
using Meta.Lib.Modules.PubSub;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Meta.Lib.Examples
{
    //public class RemotePubSubExample
    //{
    //    internal async void RunAllExamples()
    //    {
    //        Console.WriteLine("BasicExample start -----------------");
    //        await BasicExample();
    //        Console.WriteLine("");
    //    }

    //    async Task BasicExample()
    //    {
    //        int count = 0;
    //        Task Handler(MyMessage x)
    //        {
    //            count++;
    //            return Task.CompletedTask;
    //        }

    //        // Creating the server hub.
    //        // The server and the client hubs should be created in separate processes, 
    //        // this example is for demo only.
    //        var serverHub = new MetaPubSub();

    //        // Starting the hub as a server named 'Meta'.
    //        serverHub.StartServer("Meta");

    //        // Client hub creation. There are can be several hubs connected to the same server.
    //        var clientHub = new MetaPubSub();

    //        // Connecting to the remote server.
    //        await clientHub.ConnectToServer("Meta");

    //        // Subscribing to MyMessage on the server and locally at the same time.
    //        await clientHub.SubscribeOnServer<MyMessage>(Handler);

    //        // The server publishes a message.
    //        await serverHub.Publish(new MyMessage());

    //        // Client hub publishes a message and it will be received locally without being sent to the server.
    //        await clientHub.Publish(new MyMessage());

    //        // Client hub sends a message to the server where it will be published and sent back.
    //        //todo - check is it works
    //        await clientHub.PublishOnServer<MyMessage, MyMessageResponse>(new MyMessage());

    //        // All three messages should be received.
    //        Debug.Assert(count == 3);

    //        // Unsubscribing both on the server-side and locally.
    //        await clientHub.Unsubscribe<MyMessage>(Handler);
    //    }
    //}
}
