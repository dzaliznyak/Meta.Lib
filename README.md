Meta.Lib
========================

`MetaPubSub` is an implementation of the publish/subscribe pattern - when the publisher and subscriber know nothing of each other but can exchange messages. It's fast, lightweight and beside basic functionality has some cool features:

- interprocess communication - messages can be sent between different processes and computers
- awaitable methods, for example, you can await `Publish` and wait until all subscribers have finished processing the message 
- at least once delivery check - you can opt in to have an exception if no one subscribed to your message
- message filtering - you can define a predicate to subscribe only those messages you want to process
- timeout to wait for a subscriber - your message can be queued and wait until someone subscribed and processed it
- scheduling a message - your message can be queued and published after a time delay
- request-response pattern - send a message and wait for the response as a single awaitable method, without need to `Subscribe/Unsubscribe` to the response message
- cancellation token support - you can cancel scheduling or waiting for the message
- exceptions handling - all exceptions raised when a message processing by subscribers can be caught by the publisher as an `AggregateException`


`MetaPipeServer` and `MetaPipeConnection` are used to simplify inter-process communication using named pipes. The module utilizes `NamedPipeServerStream` and `NamedPipeClientStream` for message transmission. `System.Text.Json` is used for object serialization before sending. The main features of the module include:

- automatic connection recovery in case of disconnection
- transmission of strings, byte arrays, and arbitrary objects
- sending an arbitrary object and waiting for the response in a single method call.

`PubSubPipe` extends the capabilities of the `MetaPubSub` module by adding the ability to exchange messages between processes or computers.

`StateMachine` is a simple and easy to use state machine implementation. It is thread-safe and can be used in a concurrent environment. See the implementation of the `MetaPipeConnection` class for an example of usage.

# NuGet packages

To install the [Meta.Lib](https://www.nuget.org/packages/Meta.Lib/) run the following command:
```Powershell
PM> Install-Package Meta.Lib 
```

# [How to use](https://github.com/dzaliznyak/Meta.Lib/wiki/HowTo)

# [FAQ](https://github.com/dzaliznyak/Meta.Lib/wiki/FAQ)

# Changelog

## Ver. 2.0.0

Note: this version has breaking changes. See [migration guide](https://github.com/dzaliznyak/Meta.Lib/wiki/MigrationTo20).

- MetaPubSub has been separated into three modules - local `PubSub`, `Pipe`, and `PubSubPipe`. `PubSub` – is a local publisher/subscriber implementation. `Pipe` – is a wrapper on `NamedPipeServerStream` and `NamedPipeClientStream` to simplify interprocess communication. `PubSubPipe` – is a wrapper on both `PubSub` and `Pipe` which adds interprocess communication ability for `PubSub` via `Pipe`.
- Added `ConcurrentStateMachine`.
- Custom `Logger` implementation replaced with `ILogger` from the `Microsoft.Extensions.Logging` namespace.
- Removed `IPubSubMessage` interface. You don't need to derive your message classes from this interface anymore.
- Performance improvements.

## Ver. 1.1.3

Note: this version has breaking changes.

- `IPubSubMessage.Timeout` renamed to `WaitForSubscriberTimeout`.
- Added `IPubSubMessage.ResponseTimeout` - Time interval during which the response message must be received otherwise the `TimeoutException` will be thrown. Used in `IMetaPubSub.Process()` and `IMetaPubSub.ProcessOnServer()`.
- Removed parameter `millisecondsTimeout` from `IMetaPubSub.Process()` and `IMetaPubSub.ProcessOnServer()`. Use `IPubSubMessage.ResponseTimeout` instead.
- Fixed bug: timeout in `IMetaPubSub.Process()` and `IMetaPubSub.ProcessOnServer()` always use it's default value of 5 sec.


## Ver. 1.1.2

- Added `TryConnectToServer` & `TrySubscribeOnServer`.
- Added a match predicate to `SubscribeOnServer` method.
- Added `Connected/Disconnected` built-in messages.
- Added a delegate method to create a pipe with non-default parameters.


## Ver. 1.1.1

- Interprocess communication implemented.
