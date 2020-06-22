using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSub.Messages;
using System;
using System.Threading.Tasks;

namespace TestClient
{
    class Program
    {
        private static MetaPubSub hub;

        static void Main(string[] args)
        {
            RunClient();
            Console.ReadLine();
        }

        static async void RunClient()
        {
            // local hub creation
            hub = new MetaPubSub();

            // connecting the remote server
            await hub.ConnectServer("Meta");

            // subscribing
            await hub.SubscribeOnServer<PingCommand>(OnPingCommand);
        }

        static Task OnPingCommand(PingCommand arg)
        {
            Console.WriteLine($"Client >>>: ping {DateTime.Now:HH:mm:ss.fff}");
            return Task.CompletedTask;
        }
    }
}
