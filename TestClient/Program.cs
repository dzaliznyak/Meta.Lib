using Meta.Lib.Messages;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSubPipe;
using System;
using System.Threading.Tasks;

namespace TestClient
{
    class Program
    {
        static MetaPubSub hub;
        static PubSubPipeClient pipe;
        static int pingId;

        static void Main(string[] args)
        {
            RunClient();
            string line;
            while ((line = Console.ReadLine()) != "exit")
            {
                if (line == "ping" || line == "")
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await pipe.PublishOnServer(new PingCommand() { Id = pingId++.ToString() });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"PublishOnServer error: {ex.Message}");
                        }
                    });
                }
            }
        }

        static async void RunClient()
        {
            try
            {
                // local hub creation
                hub = new MetaPubSub();

                // connecting the remote server
                pipe = new PubSubPipeClient(hub, "Meta");
                await pipe.ConnectToServer();
                await pipe.SubscribeOnServer<PingResponse>();

                // subscribing
                hub.Subscribe<PingResponse>(OnPingResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static Task OnPingResponse(PingResponse response)
        {
            Console.WriteLine($"Client >>>: ping response {response.Id} {DateTime.Now:HH:mm:ss.fff}");
            return Task.CompletedTask;
        }
    }
}
