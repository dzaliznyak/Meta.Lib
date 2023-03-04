using Meta.Lib.Messages;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSubPipe;
using Microsoft.Extensions.Logging;
using System;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;

namespace TestServer
{
    class Program
    {
        static MetaPubSub hub;
        static PubSubPipeServer pipeServer;
        static ILogger logger;

        static void Main(string[] args)
        {
            using (var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Trace)
                    .AddFilter("System", LogLevel.Trace)
                    .AddFilter("TestServer.Program", LogLevel.Trace)
                    .AddConsole()
                    .AddDebug();
            }))
            {
                logger = loggerFactory.CreateLogger<Program>();
                logger.LogInformation("Logger created");
            }

            RunServer();

            Console.Write(">");
            string line;
            while ((line = Console.ReadLine()) != "exit")
            {
                try
                {
                    if (line == "stop")
                        pipeServer.StopServer();
                    else if (line == "start")
                    {
                        pipeServer.StartServer();
                        hub.Subscribe<PingCommand>(OnPing);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception: ");
                }

                Console.Write(">");
            }
        }

        static void RunServer()
        {
            hub = new MetaPubSub(logger);
            pipeServer = new PubSubPipeServer("Meta", hub, logger);

            // Servers started on the Windows process with elevated permissions need to
            // set up security to allow non-elevated processes to access the pipe.
            // Otherwise just use hub.StartServer() call
            pipeServer.StartServer(() =>
            {
                var pipeSecurity = new PipeSecurity();
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));

                var pipe = new NamedPipeServerStream("Meta", PipeDirection.InOut, 32,
                    PipeTransmissionMode.Message, PipeOptions.Asynchronous, 4096, 4096, pipeSecurity);

                return pipe;
            });
            hub.Subscribe<PingCommand>(OnPing);
        }

        static async Task OnPing(PingCommand ping)
        {
            logger.LogInformation("ping received: {Id}", ping.Id);
            await hub.Publish(new PingResponse() { Id = ping.Id });
        }
    }
}
