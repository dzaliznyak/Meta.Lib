using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSub.Messages;
using NLog;
using System;
using System.Threading.Tasks;

namespace TestServer
{
    class Program
    {
        static NLogAdapter logger;
        static MetaPubSub hub;

        static void Main(string[] args)
        {
            RunServer();

            Console.Write(">");
            string line;
            while ((line = Console.ReadLine()) != "exit")
            {
                try
                {
                    if (line == "stop")
                        hub.StopServer();
                    else if (line == "start")
                    {
                        hub.StartServer("Meta");
                        hub.Subscribe<PingCommand>(OnPing);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }

                Console.Write(">");
            }
        }

        static void RunServer()
        {
            var nLog = LogManager.GetLogger("MetaPubSub");
            logger = new NLogAdapter(nLog);
            hub = new MetaPubSub(logger);
            hub.StartServer("Meta");
            hub.Subscribe<PingCommand>(OnPing);
        }

        static async Task OnPing(PingCommand ping)
        {
            logger.Info($"ping {ping.Id}");
            await hub.Publish(new PingReplay() { Id = ping.Id });
        }

    }
}
