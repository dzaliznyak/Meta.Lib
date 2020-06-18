using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSub.Messages;
using System;
using System.Threading.Tasks;

namespace Meta.Lib.Examples
{
    public class InterprocPubSubTest
    {
        // message handler
        Task OnMyMessage(MyMessage message)
        {
            Console.WriteLine($"OnMyMessage called at {DateTime.Now:HH:mm:ss.fff}");
            //throw new Exception("OnMyMessage exception");
            return Task.CompletedTask;
        }

        Task OnMyMessage2(MyMessage2 message)
        {
            Console.WriteLine($"OnMyMessage2 called at {DateTime.Now:HH:mm:ss.fff}");
            //throw new Exception("OnMyMessage2 exception");
            return Task.CompletedTask;
        }

        internal async void RunAllExamples()
        {
            Console.WriteLine("BasicExample start -----------------");
            await BasicExample();
            Console.WriteLine("");
        }

        async Task BasicExample()
        {
            try
            {
                // creating remote hub
                var t = Task.Run(async () =>
                {
                    var hub = new MetaPubSub();
                    hub.StartServer("Meta");
                    Console.WriteLine($"Started server 'Meta' at {DateTime.Now:HH:mm:ss.fff}");

                    while (true)
                    {
                        //await Task.Delay(2000);

                        // publishing a message at the remote hub
                        try
                        {
                            await hub.Publish(new PingCommand() { Data = new byte[1024*10] });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ERROR: {ex.Message}");
                        }

                        //await hub.Publish(new MyEvent());
                    }
                });


                // creating first local hub
                var t2 = Task.Run(async () =>
                {
                    try
                    {
                        // local hub creation
                        var hub = new MetaPubSub();

                        // connecting the remote server
                        await hub.ConnectServer("Meta");

                        // subscribing to MyMessage
                        await hub.SubscribeOnServer<MyMessage2>(OnMyMessage2);

                        while (true)
                        {
                            await Task.Delay(2000);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"!!!!!!!!!!!!!! Client2 exception: {ex.Message}");
                    }
                });

                // local hub creation
                var hub = new MetaPubSub();

                // connecting the remote server
                await hub.ConnectServer("Meta");

                await Task.Delay(1000);

                // subscribing to MyMessage
                await hub.SubscribeOnServer<MyMessage>(OnMyMessage);

                // publishing a message
                //await hub.Publish(new MyMessage());
                await Task.Delay(100000);

                // unsubscribing
                hub.Unsubscribe<MyMessage>(OnMyMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"!!!!!!!!!!!!!! Client1 exception: {ex.Message}");
            }

            //return Task.CompletedTask;
        }
    }
}
