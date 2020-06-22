﻿using Meta.Lib.Modules.Logger;
using Meta.Lib.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public class PipeConnectionsManager : LogWriterBase
    {
        readonly MessageHub _hub;
        readonly Action<Type, PipeServer> _onNewPipeSubscriber;
        readonly object _serversLock = new object();

        ImmutableList<PipeServer> _servers = ImmutableList<PipeServer>.Empty;

        public string PipeName { get; private set; }

        internal PipeConnectionsManager(MessageHub hub,
            IMetaLogger log,
            Action<Type, PipeServer> onNewPipeSubscriber)
            : base(nameof(PipeConnectionsManager), log)
        {
            _hub = hub;
            _onNewPipeSubscriber = onNewPipeSubscriber;
        }

        internal void Start(string pipeName)
        {
            if (PipeName != null)
                throw new InvalidOperationException("Already started");

            PipeName = pipeName;
            StartNext(pipeName);
        }

        void StartNext(string pipeName)
        {
            var server = new PipeServer(_hub, _log, _onNewPipeSubscriber);

            server.Connected += (s, a) =>
            {
                lock (_serversLock)
                    _servers = _servers.Add((PipeServer)s);
                WriteDebugLine($"Server connected, count: {_servers.Count}");

                // start waiting for the next connection
                StartNext(pipeName);
            };

            server.Disconnected += (s, a) =>
            {
                lock (_serversLock)
                    _servers = _servers.Remove((PipeServer)s);
                WriteDebugLine($"Server disconnected, count: {_servers.Count}");
            };

            Task.Run(async () =>
            {
                try
                {
                    await server.Start(pipeName);
                }
                catch (Exception ex)
                {
                    WriteLine(ex);
                }
            });
        }

        // Sends a message to the clients
        internal async Task<bool> Put(IPubSubMessage message)
        {
            bool delivered = false;
            List<Exception> exceptions = null;

            // server.Id != message.RemoteConnectionId - should not send the  
            // message to the same pipe from which it has been received
            foreach (var server in _servers)
            {
                try
                {
                    if (server.IsConnected &&
                        //server.Id != message.RemoteConnectionId &&
                        server.IsShouldSend(message))
                    {
                        if (await server.SendMessage(message))
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
