using Meta.Lib.Examples.Shared;
using Meta.Lib.Exceptions;
using Meta.Lib.Modules.PubSub;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Meta.Lib.Examples
{
    public class PubSubExample
    {
        // message handler
        Task OnMyMessage(MyMessage message)
        {
            Console.WriteLine($"OnMyMessage called at {DateTime.Now:HH:mm:ss.fff}");
            return Task.CompletedTask;
        }

        Task OnMyMessageHandlerWithException(MyMessage message)
        {
            throw new Exception("Some error occurred");
        }

        public async void RunAllExamples()
        {
            Console.WriteLine("BasicExample start -----------------");
            await BasicExample();
            Console.WriteLine("");

            Console.WriteLine("ExceptionHandling start -----------------");
            await ExceptionHandlingExample();
            Console.WriteLine("");

            Console.WriteLine("AtLeastOnceDeliveryExample start -----------------");
            await AtLeastOnceDeliveryExample();
            Console.WriteLine("");

            Console.WriteLine("MessageFilteringExample start -----------------");
            await MessageFilteringExample();
            Console.WriteLine("");

            Console.WriteLine("DeliverAtLeastOnceDelayedExample start -----------------");
            await DeliverAtLeastOnceDelayedExample();
            Console.WriteLine("");

            Console.WriteLine("ScheduleExample start -----------------");
            await ScheduleExample();
            Console.WriteLine("");

            Console.WriteLine("WhenExample start -----------------");
            await WhenExample();
            Console.WriteLine("");

            Console.WriteLine("ProcessExample start -----------------");
            await ProcessExample();
            Console.WriteLine("");

            Console.WriteLine("CancellationExample start -----------------");
            await CancellationExample();
            Console.WriteLine("");

            Console.WriteLine("End of examples");
        }

        async Task BasicExample()
        {
            // hub creation
            var hub = new MetaPubSub();

            // subscribing to MyMessage
            hub.Subscribe<MyMessage>(OnMyMessage);

            // publishing a message
            await hub.Publish(new MyMessage());

            // unsubscribing
            hub.Unsubscribe<MyMessage>(OnMyMessage);
        }

        // exceptions handling
        async Task ExceptionHandlingExample()
        {
            var hub = new MetaPubSub();
            var options = new PubSubOptions() { DeliverAtLeastOnce = true, WaitForSubscriberTimeout = 0 };
            var message = new MyMessage();

            try
            {
                // publishing a message when no one subscribed - NoSubscribersException
                await hub.Publish(message, options);
            }
            catch (NoSubscribersException ex)
            {
                // No one is subscribed to this message and (DeliverAtLeastOnce == true and WaitForSubscriberTimeout == 0)
                Console.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
            }


            try
            {
                // publishing a message when no one subscribed and WaitForSubscriberTimeout > 0 - TimeoutException
                options.WaitForSubscriberTimeout = 100;
                await hub.Publish(message, options);
            }
            catch (TimeoutException ex)
            {
                // No one is subscribed to this message and (DeliverAtLeastOnce == true and WaitForSubscriberTimeout > 0)
                Console.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
            }


            try
            {
                hub.Subscribe<MyMessage>(OnMyMessageHandlerWithException);

                // publishing a message
                await hub.Publish(message, options);
            }
            catch (AggregateException ex)
            {
                // When a message is processed by subscribers, any exceptions that are raised
                // can be caught by the publisher as an AggregateException.
                // Even if some subscribers throw an exception, the others
                // will still continue to process the message.
                Console.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
                foreach (var innerEx in ex.InnerExceptions)
                {
                    Console.WriteLine($"\tInner Exception {innerEx.GetType()}: {innerEx.Message}");
                }
            }

            hub.Unsubscribe<MyMessage>(OnMyMessageHandlerWithException);
        }

        // at least once delivery check
        async Task AtLeastOnceDeliveryExample()
        {
            var hub = new MetaPubSub();

            // if this not set, NoSubscribersException will not be thrown
            var options = new PubSubOptions() { DeliverAtLeastOnce = true };

            var message = new MyMessage();

            try
            {
                // publishing a message when no one is subscribed
                await hub.Publish(message, options);
            }
            catch (NoSubscribersException ex)
            {
                // no one is listening
                Console.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
            }

            hub.Subscribe<MyMessage>(OnMyMessage);
            await hub.Publish(message, options);
            hub.Unsubscribe<MyMessage>(OnMyMessage);
        }

        // message filtering - you can define a predicate to subscribe only those messages you want to process
        async Task MessageFilteringExample()
        {
            var hub = new MetaPubSub();

            // subscribing to MyMessage with a predicate
            hub.Subscribe<MyMessage>(OnMyMessage, m => m.Version > new Version(1, 0));

            // this message will be filtered and not handled
            var message1 = new MyMessage
            {
                Version = new Version(1, 0)
            };
            await hub.Publish(message1);

            // this message will be handled
            var message2 = new MyMessage
            {
                Version = new Version(1, 1)
            };

            await hub.Publish(message2);
        }

        // timeout to wait for a subscriber - Your message can be added to a queue and will wait there until someone subscribes to it and processes it.
        public async Task DeliverAtLeastOnceDelayedExample()
        {
            var hub = new MetaPubSub();

            // a subscriber that will subscribe after the message has been published 
            var t = Task.Run(async () =>
            {
                await Task.Delay(1500);
                Console.WriteLine($"Subscribed to MyMessage at {DateTime.Now:HH:mm:ss.fff}");
                hub.Subscribe<MyMessage>(OnMyMessage);
            });

            Console.WriteLine($"Start publishing and awaiting at {DateTime.Now:HH:mm:ss.fff}");
            // this method will wait until the subscriber receives the message or until timeout expired (10 seconds)
            await hub.Publish(new MyMessage(), new PubSubOptions() { DeliverAtLeastOnce = true, WaitForSubscriberTimeout = 10_000 });
            Console.WriteLine($"End awaiting at {DateTime.Now:HH:mm:ss.fff}");

            hub.Unsubscribe<MyMessage>(OnMyMessage);
        }

        // scheduling a message - your message can be queued and published after a time delay
        public async Task ScheduleExample()
        {
            var hub = new MetaPubSub();

            hub.Subscribe<MyMessage>(OnMyMessage);

            // The message will be published after 3 seconds delay and after that, it can wait another 500 ms for a subscriber.
            // When using Schedule method there is no way to receive NoSubscriberException or AggregateException.
            hub.Schedule(new MyMessage(), millisecondsDelay: 3000, new PubSubOptions() { DeliverAtLeastOnce = true, WaitForSubscriberTimeout = 1500 });
            Console.WriteLine($"Message scheduled at {DateTime.Now:HH:mm:ss.fff}, delay - 3 sec");

            // waiting before unsubscribing
            await Task.Delay(3500);
            hub.Unsubscribe<MyMessage>(OnMyMessage);
        }

        // asynchronous waiting for a specified message by a single method call, without need to Subscribe/Unsubscribe to this message
        public static async Task WhenExample()
        {
            var hub = new MetaPubSub();

            // publishing a MyEvent with 500 ms delay
            var t = Task.Run(async () =>
            {
                await Task.Delay(500);
                await hub.Publish(new MyEvent());
            });

            try
            {
                // This method will wait for MyEvent one second.
                // If the event will not arrive in a specified time period the TimeoutException will be thrown.
                MyEvent res = await hub.When<MyEvent>(millisecondsTimeout: 1000);
                Console.WriteLine($"Received MyEvent at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
            }
        }

        // request-response pattern - send a message and wait for the response as a single awaitable method, without need to Subscribe/Unsubscribe to the response message
        public static async Task ProcessExample()
        {
            var hub = new MetaPubSub();

            // This handler should be placed somewhere in another module.
            // It processes MyMessage and publishes a MyEvent as a result. 
            Task Handler(MyMessage x)
            {
                hub.Publish(new MyEvent());
                return Task.CompletedTask;
            }
            hub.Subscribe<MyMessage>(Handler);

            try
            {
                // This method will publish MyMessage and wait for MyEvent one second.
                // If the event will not arrive in a specified timeout the TimeoutException will be thrown.
                var message = new MyMessage();
                MyEvent res = await hub.Process<MyMessage, MyEvent>(message, responseTimeoutMs: 1000, new PubSubOptions() { WaitForSubscriberTimeout = 100 });
                Console.WriteLine($"Received MyEvent at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (NoSubscribersException ex)
            {
                // no one is listening
                Console.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
            }
        }

        // cancellation token support - you can cancel scheduling or waiting for the message
        public static async Task CancellationExample()
        {
            var hub = new MetaPubSub();
            var cts = new CancellationTokenSource();

            // publish an event after 100 ms
            var t = Task.Run(async () =>
            {
                await Task.Delay(100);
                await hub.Publish(new MyEvent());
            });

            // cancel waiting after 50 ms
            var t2 = Task.Run(async () =>
            {
                await Task.Delay(50);
                cts.Cancel();
            });


            try
            {
                var res = await hub.When<MyEvent>(millisecondsTimeout: 200, match: null, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Waiting for MyEvent has been canceled");
            }
        }

    }
}
