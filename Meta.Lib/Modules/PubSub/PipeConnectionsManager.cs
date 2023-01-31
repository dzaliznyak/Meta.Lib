using Meta.Lib.Modules.PubSub.Messages;
using Meta.Lib.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public class PipeConnectionsManager
    {
        readonly ILogger _logger;
        readonly MessageHub _hub;
        readonly Action<Type, PipeServer> _onNewPipeSubscriber;
        readonly object _serversLock = new object();

        ImmutableList<PipeServer> _servers = ImmutableList<PipeServer>.Empty;
        PipeServer _pendingServer;

        public string PipeName { get; private set; }

        internal PipeConnectionsManager(MessageHub hub,
            ILogger logger,
            Action<Type, PipeServer> onNewPipeSubscriber)
        {
            _logger = logger;
            _hub = hub;
            _onNewPipeSubscriber = onNewPipeSubscriber;
        }

        internal void Start(string pipeName, Func<NamedPipeServerStream> configure)
        {
            if (PipeName != null)
                throw new InvalidOperationException("Already started");

            PipeName = pipeName;
            StartNext(pipeName, configure);

            _logger?.LogInformation("Started server '{Name}', waiting for connections...", pipeName);
        }

        internal void Stop()
        {
            _pendingServer.Stop();

            foreach (var server in _servers)
            {
                server.Stop();
            }

            PipeName = null;
            _servers = ImmutableList<PipeServer>.Empty;
        }

        void StartNext(string pipeName, Func<NamedPipeServerStream> configure)
        {
            _pendingServer = new PipeServer(_hub, _logger, _onNewPipeSubscriber);

            _pendingServer.Connected += async (s, a) =>
            {
                try
                {
                    lock (_serversLock) //todo
                    {
                        _servers = _servers.Add((PipeServer)s);
                    }

                    _logger?.LogInformation("Client connected, total count: {Count}", _servers.Count);

                    // start waiting for the next connection
                    StartNext(pipeName, configure);

                    await _hub.Publish(new RemoteClientConnectedEvent()
                    {
                        Timestamp = DateTime.Now,
                        TotalClientsCount = _servers.Count
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "PipeServer.Connected() error");
                }
            };

            _pendingServer.Disconnected += async (s, a) =>
            {
                try
                {
                    lock (_serversLock)
                        _servers = _servers.Remove((PipeServer)s);

                    _logger?.LogInformation("Client disconnected, remained: {Count}", _servers.Count);

                    await _hub.Publish(new RemoteClientDisconnectedEvent()
                    {
                        Timestamp = DateTime.Now,
                        TotalClientsCount = _servers.Count
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "PipeServer.Disconnected() error");
                }
            };

            Task.Run(async () =>
            {
                try
                {
                    await _pendingServer.Start(pipeName, configure);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to start a pipe server");
                }
            });
        }

        // Sends a message to the clients
        internal async Task<bool> Put(IPubSubMessage message)
        {
            bool delivered = false;
            List<Exception> exceptions = null;

            // server.Id != message.RemoteConnectionId - should not send the  
            // message to the same pipe from which it has been received (no echo)
            foreach (var server in _servers)
            {
                try
                {
                    if (server.IsConnected &&
                        //server.Id != message.RemoteConnectionId &&
                        server.IsShouldSend(message))
                    {
                        if (await server.SendMessage(message, PipeMessageType.Message))
                            delivered = true;
                    }
                }
                catch (Exception ex)
                {
                    if (exceptions == null)
                        exceptions = new List<Exception>();
                    exceptions.Add(ex);
                }
            }

            if (exceptions != null)
                throw new AggregateException(exceptions).Fix();

            return delivered;
        }

    }
}
