using System;
using System.Threading.Tasks;

namespace TestClient;

class Program
{
    static void Main(string[] args)
    {
        string line;
        while ((line = Console.ReadLine()) != "exit")
        {
            if (line == "ping" || line == "")
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await using var client = new Client();
                        await client.Init();
                        await client.Ping();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"PublishOnServer error: {ex.Message}");
                    }
                });
            }
            else if (line == "test")
            {
                for (int i = 0; i < 5; i++)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await using var client = new Client();
                            await client.Init();

                            for (int i = 0; i < 100; i++)
                            {
                                await client.Ping();
                            }

                            // first Dispose will unsubscribe from server
                            // so we need delay to finish all tasks
                            await Task.Delay(1000);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"PublishOnServer error: {ex.Message}");
                        }
                    });
                }
            }
        }
    }
}
