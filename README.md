# Meta.Lib

`MetaPubSub` is an implementation of the publish/subscribe pattern, enabling message exchange between publishers and subscribers without direct dependencies. It is fast, lightweight, and offers several advanced features:

- **Interprocess communication** – Messages can be sent between different processes and computers.
- **Awaitable methods** – You can `await Publish` and wait until all subscribers have processed the message.
- **At-least-once delivery check** – Optionally, throw an exception if no subscribers receive the message.
- **Message filtering** – Subscribe only to specific messages using a predicate.
- **Subscriber wait timeout** – Messages can be queued until a subscriber processes them.
- **Scheduled messages** – Messages can be queued and published after a delay.
- **Request-response pattern** – Send a message and wait for a response in a single awaitable method without needing to `Subscribe/Unsubscribe`.
- **Cancellation token support** – Cancel scheduled messages or waiting for a response.
- **Exception handling** – Exceptions raised during message processing can be caught by the publisher as an `AggregateException`.

### `MetaPipeServer` and `MetaPipeConnection`

These components simplify interprocess communication using named pipes. They leverage `NamedPipeServerStream` and `NamedPipeClientStream` for message transmission, with `System.Text.Json` for object serialization. Features include:

- **Automatic connection recovery** in case of disconnection.
- **Support for multiple data types**, including strings, byte arrays, and objects.
- **Synchronous request-response support** – Send an object and wait for a response in a single method call.

### `PubSubPipe`

`PubSubPipe` extends `MetaPubSub`, enabling message exchange between processes or computers.

### `StateMachine`

A simple, thread-safe state machine implementation suitable for concurrent environments. See `MetaPipeConnection` for a usage example.

## NuGet Package

To install [Meta.Lib](https://www.nuget.org/packages/Meta.Lib/), run:

```powershell
PM> Install-Package Meta.Lib
```

## [How to Use](https://github.com/dzaliznyak/Meta.Lib/wiki/HowTo)

## [FAQ](https://github.com/dzaliznyak/Meta.Lib/wiki/FAQ)

---

## Changelog

### Version 2.0.2

- Added MetaPubSub property to IPubSubPipeClient.
- Added Connected and Disconnected events.

### Version 2.0.1

- Updated packages to .Net 9.

### Version 2.0.0

**Breaking changes** – See the [migration guide](https://github.com/dzaliznyak/Meta.Lib/wiki/MigrationTo20).

- `MetaPubSub` has been split into three modules:
  - `PubSub` – Local publish/subscribe implementation.
  - `Pipe` – Wrapper for `NamedPipeServerStream` and `NamedPipeClientStream` to simplify interprocess communication.
  - `PubSubPipe` – Combines `PubSub` and `Pipe` to enable interprocess communication for `PubSub`.
- Added `ConcurrentStateMachine`.
- Replaced custom `Logger` with `ILogger` from `Microsoft.Extensions.Logging`.
- Removed `IPubSubMessage` interface (messages no longer need to implement it).
- Performance improvements.

### Version 1.1.3

**Breaking changes:**

- Renamed `IPubSubMessage.Timeout` to `WaitForSubscriberTimeout`.
- Added `IPubSubMessage.ResponseTimeout` – Defines the time interval to receive a response before throwing a `TimeoutException`. Used in `IMetaPubSub.Process()` and `IMetaPubSub.ProcessOnServer()`.
- Removed `millisecondsTimeout` from `IMetaPubSub.Process()` and `IMetaPubSub.ProcessOnServer()`. Use `IPubSubMessage.ResponseTimeout` instead.
- Fixed bug where `IMetaPubSub.Process()` and `IMetaPubSub.ProcessOnServer()` always used the default timeout of 5 seconds.

### Version 1.1.2

- Added `TryConnectToServer` and `TrySubscribeOnServer`.
- Added a match predicate to `SubscribeOnServer`.
- Introduced built-in `Connected/Disconnected` messages.
- Added a delegate method for creating pipes with custom parameters.

### Version 1.1.1

- Implemented interprocess communication.

