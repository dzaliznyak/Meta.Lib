using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSub.Messages;
using System;
using System.Threading.Tasks;

namespace TestServer
{
    class Program
    {
        static MetaPubSub hub;

        static void Main(string[] args)
        {
            RunServer();
            Console.ReadLine();
        }

        static void RunServer()
        {
            hub = new MetaPubSub();
            hub.StartServer("Meta");
            hub.Subscribe<PingCommand>(OnPing);
        }

        static async Task OnPing(PingCommand ping)
        {
            Console.WriteLine($"ping {ping.Id}");
            await hub.Publish(new PingReplay() { Id = ping.Id });
        }

    }
}
