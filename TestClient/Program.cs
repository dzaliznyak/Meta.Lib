﻿using Meta.Lib.Modules.PubSub;
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
            string line;
            while ((line = Console.ReadLine()) != "exit")
            {
                if (line == "ping")
                    hub.PublishOnServer(new PingCommand());
            }
        }

        static async void RunClient()
        {
            try
            {
                // local hub creation
                hub = new MetaPubSub();

                // connecting the remote server
                await hub.ConnectToServer("Meta");

                // subscribing
                await hub.SubscribeOnServer<PingCommand>(OnPingCommand);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static Task OnPingCommand(PingCommand arg)
        {
            Console.WriteLine($"Client >>>: ping {DateTime.Now:HH:mm:ss.fff}");
            return Task.CompletedTask;
        }
    }
}
