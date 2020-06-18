using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSub.Messages;
using System;
using System.Threading.Tasks;

namespace TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            RunClient();
            Console.ReadLine();
        }

        static async void RunClient()
        {
            // local hub creation
            var hub = new MetaPubSub();

            // connecting the remote server
            await hub.ConnectServer("Meta");

            // subscribing
            await hub.SubscribeOnServer<PingCommand>(OnPingCommand);

            while (true)
            {
                await Task.Delay(2000);
            }
        }

        static Task OnPingCommand(PingCommand arg)
        {
            Console.WriteLine($"Client >>>: ping {DateTime.Now:HH:mm:ss.fff}");
            return Task.CompletedTask;
        }
    }
}
