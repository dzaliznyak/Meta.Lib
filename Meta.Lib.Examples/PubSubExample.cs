using Meta.Lib.Modules.Logger;
using Meta.Lib.Modules.PubSub;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Examples
{
    public class MyMessage : PubSubMessageBase
    {
        public int SomeId { get; set; }
        public int DeliveredCount { get; set; }
        public MetaLogErrorSeverity LogSeverity { get; set; }
    }

    public class MyEvent : MyMessage
    {
    }

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

        // exceptions handling - all exceptions raised when a message processing by subscribers can be caught by the publisher as an AggregateException
        async Task ExceptionHandlingExample()
        {
            var hub = new MetaPubSub();
            hub.Subscribe<MyMessage>(OnMyMessageHandlerWithException);

            try
            {
                // publishing a message
                await hub.Publish(new MyMessage());
            }
            catch (AggregateException ex)
            {
                // All exceptions raised when a message processing by subscribers 
                // can be caught by the publisher as an AggregateException.
                // If some of the subscribers throw an exception, other subscribers 
                // continues to process the message.
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

            var message = new MyMessage
            {
                // if this not set, NoSubscribersException will not be thrown
                DeliverAtLeastOnce = true
            };

            try
            {
                // publishing a message when no one is subscribed
                await hub.Publish(message);
            }
            catch (NoSubscribersException ex)
            {
                // no one is listening
                Console.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
            }

            hub.Subscribe<MyMessage>(OnMyMessage);
            await hub.Publish(message);
            hub.Unsubscribe<MyMessage>(OnMyMessage);
        }

        // message filtering - you can define a predicate to subscribe only those messages you want to process
        async Task MessageFilteringExample()
        {
            var hub = new MetaPubSub();

            // subscribing to MyMessage with a predicate that selects only error and critical messages
            hub.Subscribe<MyMessage>(OnMyMessage, m =>
                m.LogSeverity == MetaLogErrorSeverity.Error ||
                m.LogSeverity == MetaLogErrorSeverity.Fatal);

            // this message will be filtered and not handled
            var message1 = new MyMessage
            {
                LogSeverity = MetaLogErrorSeverity.Information
            };
            await hub.Publish(message1);

            // this message will be handled
            var message2 = new MyMessage
            {
                LogSeverity = MetaLogErrorSeverity.Error
            };

            await hub.Publish(message2);
        }

        // timeout to wait for a subscriber - your message can be queued and wait until someone subscribed and processed it
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

            // the message has the 10 seconds timeout and can wait until the subscriber come
            var message = new MyMessage
            {
                DeliverAtLeastOnce = true, // this must be set to true
                Timeout = 10_000
            };

            Console.WriteLine($"Start publishing and awaiting at {DateTime.Now:HH:mm:ss.fff}");
            // this method will wait until the subscriber receives the message or until timeout expired (10 seconds)
            await hub.Publish(message);
            Console.WriteLine($"End awaiting at {DateTime.Now:HH:mm:ss.fff}");

            hub.Unsubscribe<MyMessage>(OnMyMessage);
        }

        // scheduling a message - your message can be queued and published after a time delay
        public async Task ScheduleExample()
        {
            var hub = new MetaPubSub();

            hub.Subscribe<MyMessage>(OnMyMessage);

            var message = new MyMessage
            {
                DeliverAtLeastOnce = true,
                Timeout = 1500
            };

            // The message will be published after 3 seconds delay and after that, it can wait another 500 ms for a subscriber.
            // When using Schedule method there is no way to receive NoSubscriberException or AggregateException.
            hub.Schedule(message, millisecondsDelay: 3000);
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
                // If the event will not arrive in a specified timeout the TimeoutException will be thrown.
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

                var message = new MyMessage { DeliverAtLeastOnce = true, Timeout = 100 };
                MyEvent res = await hub.Process<MyEvent>(message, millisecondsTimeout: 1000);
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
                var res = await hub.When<MyEvent>(millisecondsTimeout: 200, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Waiting for MyEvent has been canceled");
            }
        }

    }
}
