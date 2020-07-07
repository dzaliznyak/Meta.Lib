MetaPubSub
========================

MetaPubSub is an implementation of the publish/subscribe pattern - when the publisher and subscriber know nothing of each other but can exchange messages. It's fast, lightweight and beside basic functionality has some cool features:

- **New: interprocess communication** - messages can be sent between different processes and computers
- the independent order of the Subscribe and Publish methods
- awaitable methods, for example, you can await Publish and wait until all subscribers have finished processing the message 
- at least once delivery check - you can opt in to have an exception if no one subscribed to your message
- message filtering - you can define a predicate to subscribe only those messages you want to process
- timeout to wait for a subscriber - your message can be queued and wait until someone subscribed and processed it
- scheduling a message - your message can be queued and published after a time delay
- asynchronous waiting for a specified message by a single method call, without need to Subscribe/Unsubscribe to this message
- request-response pattern - send a message and wait for the response as a single awaitable method, without need to Subscribe/Unsubscribe to the response message
- cancellation token support - you can cancel scheduling or waiting for the message
- exceptions handling - all exceptions raised when a message processing by subscribers can be caught by the publisher as an AggregateException

# NuGet packages

To install the [Meta.Lib](https://www.nuget.org/packages/Meta.Lib/) run the following command:
```Powershell
PM> Install-Package Meta.Lib 
```

# Logging
MetaPubSub logs it's messaging to Console and Trace outputs by default. But you can assign any logger on your choice. The TestServer demo application contains a sample of how to use MetaPubSub with NLog.

# How to use

## Preparing a message class
Each message class must be derived from the IPubSubMessage interface:

```c#
public interface IPubSubMessage
{
    bool DeliverAtLeastOnce { get; }
    int Timeout { get; }
    string RemoteConnectionId { get; set; }
}
```

Also, you can derive it from PubSubMessageBase, which declaration is as follows:

```c#
public class PubSubMessageBase : IPubSubMessage
{
    public bool DeliverAtLeastOnce { get; set; }

    public int Timeout { get; set; }

    public string RemoteConnectionId { get; set; }
}
```

Or you can define your own base class:

```c#
public class MessageBase : IPubSubMessage
{
    // set this to be the default value for all derived classes
    public bool DeliverAtLeastOnce => true;

    // default timeout in milliseconds
    public int Timeout => 1000;

    // required for internal message processing, leave it as is
    public string RemoteConnectionId { get; set; }
}
```

So your message declaration should look like this:

```c#
public class MyMessage : MessageBase // or PubSubMessageBase
{
    public string SomeData { get; }

    public MyMessage(string data)
    {
        SomeData = data;
    }
}
```

## Hub creation
```c#
// hub creation
var hub = new MetaPubSub();

// subscribing to MyMessage
hub.Subscribe<MyMessage>(OnMyMessage);

// publishing a message
await hub.Publish(new MyMessage());

// unsubscribing
hub.Unsubscribe<MyMessage>(OnMyMessage);
```

## Exceptions handling
```c#
var hub = new MetaPubSub();

try
{
    var message = new MyMessage
    {
        DeliverAtLeastOnce = true,
    };

    // publishing a message when no one subscribed - NoSubscribersException
    //await hub.Publish(message);

    // publishing a message when no one subscribed and Timeout > 0 - TimeoutException
    //message.Timeout = 100;
    //await hub.Publish(message);

    hub.Subscribe<MyMessage>(OnMyMessageHandlerWithException);

    // publishing a message
    await hub.Publish(message);
}
catch (NoSubscribersException ex)
{
    // No one is subscribed to this message and (message.DeliverAtLeastOnce == true and message.Timeout == 0)
    Console.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
}
catch (TimeoutException ex)
{
    // No one is subscribed to this message and (message.DeliverAtLeastOnce == true and message.Timeout > 0)
    Console.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
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

await hub.Unsubscribe<MyMessage>(OnMyMessageHandlerWithException);
```

## At least once delivery check
```c#
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
```

## Message filtering
You can define a predicate to subscribe only those messages you want to process.
```c#
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
```

## Timeout to wait for a subscriber
Your message can be queued and wait until someone subscribed and processed it.
```c#
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
```

## Scheduling a message
Your message can be queued and published after a time delay.
```c#
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
```

## Asynchronous waiting for a specified message
```c#
var hub = new MetaPubSub();

// publishing a MyEvent with 500 ms delay
var t = Task.Run(async () =>
{
    await Task.Delay(500);
    await hub.Publish(new MyEvent());
});

try
{
    // This method will wait for MyEvent one second, without need to Subscribe/Unsubscribe to this message.
    // If the event will not arrive in a specified timeout the TimeoutException will be thrown.
    MyEvent res = await hub.When<MyEvent>(millisecondsTimeout: 1000);
    Console.WriteLine($"Received MyEvent at {DateTime.Now:HH:mm:ss.fff}");
}
catch (TimeoutException ex)
{
    Console.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
}
```

## Request-response pattern
Send a message and wait for the response as a single awaitable method, without need to Subscribe/Unsubscribe to the response message.
```c#
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
```

## Cancellation token support
You can cancel scheduling or waiting for the message.
```c#
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
```


## Client-server mode
You can start one hub as a server and connect to it several clients hubs from different processes and even different computers. Interprocess communication made on named pipes. If client disconnected from the server for some reason it tries to reconnect automatically.
```c#
int count = 0;
Task Handler(MyMessage x)
{
    count++;
    return Task.CompletedTask;
}

// Creating the server hub.
// The server and the client hubs should be created in separate processes, 
// this example is for demo only.
var serverHub = new MetaPubSub();

// Starting the hub as a server named 'Meta'.
serverHub.StartServer("Meta");

// Client hub creation. There are can be several hubs connected to the same server.
var clientHub = new MetaPubSub();

// Connecting to the remote server.
await clientHub.ConnectToServer("Meta");

// Subscribing to MyMessage on the server and locally at the same time.
await clientHub.SubscribeOnServer<MyMessage>(Handler);

// The server publishes a message.
await serverHub.Publish(new MyMessage());

// Client hub publishes a message and it will be received locally without being sent to the server.
await clientHub.Publish(new MyMessage());

// Client hub sends a message to the server where it will be published and sent back.
await clientHub.PublishOnServer(new MyMessage());

// All three messages should be received.
Debug.Assert(count == 3);

// Unsubscribing both on the server-side and locally.
await clientHub.Unsubscribe<MyMessage>(Handler);
```
