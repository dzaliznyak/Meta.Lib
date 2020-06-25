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
            string line;
            while ((line = Console.ReadLine()) != "exit")
            {
                if (line == "stop")
                    hub.StopServer();
                else if (line == "start")
                {
                    hub.StartServer("Meta");
                    hub.Subscribe<PingCommand>(OnPing);
                }
            }
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
