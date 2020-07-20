using Meta.Lib.Modules.Logger;
using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class ConnectionScope
    {
        public string PipeName { get; set; }
        public string ServerName { get; set; }
        public int MillisecondsTimeout { get; set; }
        public int ReconnectionPeriod { get; set; }
        public CancellationTokenSource Cts { get; set; }
    }

    internal class RemotePubSubProxy : PipeConnection
    {
        readonly ConcurrentDictionary<Type, string> _subscribedTypes =
            new ConcurrentDictionary<Type, string>();
        readonly object _lock = new object();

        public bool ConnectedOrConnecting => _connectionScope != null;

        ConnectionScope _connectionScope;
        int _isReconnecting;

        public RemotePubSubProxy(MessageHub hub, IMetaLogger logger)
            : base(hub, logger)
        {
        }

        internal async Task<bool> Connect(string pipeName,
            int millisecondsTimeout = 5_000, int reconnectionPeriod = 5_000, string serverName = ".")
        {
            ConnectionScope scope;
            lock (_lock)
            {
                if (_connectionScope != null)
                    throw new InvalidOperationException("The ConnectToServer() method has been already called. You need to call the Disconnect() method before attempting to establish a new connection.");

                scope = _connectionScope = new ConnectionScope
                {
                    PipeName = pipeName,
                    ServerName = serverName,
                    MillisecondsTimeout = millisecondsTimeout,
                    ReconnectionPeriod = reconnectionPeriod,
                    Cts = new CancellationTokenSource()
                };
            }

            _logger.Info($"Connecting to server '{_connectionScope.ServerName}{_connectionScope.PipeName}'... ");

            var pipe = await ConnectPipe(scope);

            if (pipe == null)
            {
                StartReconnectionLoop(scope);
                return false;
            }

            await Init(pipe);

            return true;
        }

        async Task Init(NamedPipeClientStream pipe)
        {
            InitPipeConnection(pipe);

            StartReadLoop();

            await ResubscribeAllMessages();

            FireConnectedEvent();

            _logger.Info($"Connected to server '{_connectionScope.ServerName}{_connectionScope.PipeName}'");
        }

        async Task<NamedPipeClientStream> ConnectPipe(ConnectionScope connectionScope)
        {
            var pipe = new NamedPipeClientStream(
                connectionScope.ServerName, 
                connectionScope.PipeName,
                PipeDirection.InOut, 
                PipeOptions.Asynchronous);

            try
            {
                await pipe.ConnectAsync(connectionScope.MillisecondsTimeout, connectionScope.Cts.Token);
                Disconnected += RemotePubSubProxy_Disconnected;
                return pipe;
            }
            catch (Exception)
            {
                pipe.Dispose();

                if (connectionScope.ReconnectionPeriod > 0)
                    return null;

                throw;
            }
        }

        public override void Disconnect()
        {
            ConnectionScope scope = null;
            lock (_lock)
            {
                if (_connectionScope == null)
                    throw new InvalidOperationException("The ConnectToServer() method has not been called.");

                scope = _connectionScope;
                _connectionScope = null;
            }

            scope.Cts.Cancel();

            Disconnected -= RemotePubSubProxy_Disconnected;
            base.Disconnect();
        }

        void RemotePubSubProxy_Disconnected(object sender, EventArgs e)
        {
            var scope = _connectionScope;
            if (scope != null)
                StartReconnectionLoop(scope);
        }

        internal void StartReconnectionLoop(ConnectionScope scope)
        {
            if (IsConnected)
                throw new InvalidOperationException("Already connected");

            if (Interlocked.CompareExchange(ref _isReconnecting, 1, 0) == 0)
            {
                _logger.Info($"Starting reconnection loop to the server '{scope.ServerName}{scope.PipeName}'");

                Task.Run(async () =>
                {
                    await Task.Delay(scope.ReconnectionPeriod, scope.Cts.Token);

                    bool connected = false;
                    while (!connected && !scope.Cts.IsCancellationRequested)
                    {
                        try
                        {
                            var pipe = await ConnectPipe(scope);
                            if (pipe != null)
                            {
                                await Init(pipe);
                                connected = true;
                            }
                        }
                        catch (Exception)
                        {
                            await Task.Delay(scope.ReconnectionPeriod, scope.Cts.Token);
                        }
                    }

                    _logger.Info($"Finished reconnection loop to the server '{scope.ServerName}{scope.PipeName}'");

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
            _subscribedTypes.TryAdd(type, type.AssemblyQualifiedName);
            await SendMessage(type.AssemblyQualifiedName, PipeMessageType.Subscribe);
            _logger.Trace($"Subscribed '{type.Name}'");
        }

        public async Task<bool> TrySubscribe<TMessage>()
        {
            var type = typeof(TMessage);
            try
            {
                _subscribedTypes.TryAdd(type, type.AssemblyQualifiedName);
                await SendMessage(type.AssemblyQualifiedName, PipeMessageType.Subscribe);
                _logger.Trace($"Subscribed '{type.Name}'");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Trace($"Failed to subscribe to '{type.Name}': {ex.Message}");
                return false;
            }
        }

        public async Task Unsubscribe(Type type)
        {
            if (ConnectedOrConnecting)//todo
            {
                await SendMessage(type.AssemblyQualifiedName, PipeMessageType.Unsubscribe);
                _subscribedTypes.TryRemove(type, out var _);
                _logger.Trace($"Unsubscribed '{type.Name}'");
            }
        }

    }
}
