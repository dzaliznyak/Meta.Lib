using Meta.Lib.Modules.Logger;
using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class RemotePubSubProxy : PipeConnection
    {
        readonly string _pipeName;
        readonly string _serverName;
        readonly ConcurrentDictionary<Type, string> _subscribedTypes =
            new ConcurrentDictionary<Type, string>();

        int _isReconnecting;

        public RemotePubSubProxy(MessageHub hub, IMetaLogger logger, string pipeName, string serverName = ".")
            : base(hub, logger)
        {
            _pipeName = pipeName;
            _serverName = serverName;
        }

        internal async Task Connect(int millisecondsTimeout = 5_000)
        {
            _logger.Info($"Connecting to server '{_serverName}{_pipeName}'... "); // {Environment.StackTrace}

            var pipe = new NamedPipeClientStream(_serverName, _pipeName,
                PipeDirection.InOut, PipeOptions.Asynchronous);

            try
            {
                await pipe.ConnectAsync(millisecondsTimeout);
            }
            catch (Exception)
            {
                pipe.Dispose();
                throw;
            }

            Init(pipe);

            StartReadLoop();

            await ResubscribeAllMessages();

            FireConnectedEvent();

            _logger.Info($"Connected to server '{_serverName}{_pipeName}'");
        }

        internal void StartReconnectionLoop(int millisecondsTimeout)
        {
            if (IsConnected)
                throw new InvalidOperationException("Already connected");

            if (Interlocked.CompareExchange(ref _isReconnecting, 1, 0) == 0)
            {
                _logger.Info($"Starting reconnection loop to the server '{_serverName}{_pipeName}'");

                Task.Run(async () =>
                {
                    await Task.Delay(millisecondsTimeout);

                    bool connected = false;
                    while (!connected)
                    {
                        try
                        {
                            await Connect(millisecondsTimeout);
                            connected = true;
                        }
                        catch (Exception)
                        {
                            await Task.Delay(millisecondsTimeout);
                        }
                    }

                    Interlocked.Exchange(ref _isReconnecting, 0);
                });
            }
        }

        async Task ResubscribeAllMessages()
        {
            foreach (var item in _subscribedTypes.Values)
                await SendMessage(item, PipeMessageType.Subscribe);
        }

        public async Task Subscribe<TMessage>()
        {
            var type = typeof(TMessage);
            await SendMessage(type.AssemblyQualifiedName, PipeMessageType.Subscribe);
            _subscribedTypes.TryAdd(type, type.AssemblyQualifiedName);
            _logger.Trace($"Subscribed '{type.Name}'");
        }

        public async Task Unsubscribe(Type type)
        {
            await SendMessage(type.AssemblyQualifiedName, PipeMessageType.Unsubscribe);
            _subscribedTypes.TryRemove(type, out var _);
            _logger.Trace($"Unsubscribed '{type.Name}'");
        }

    }
}
