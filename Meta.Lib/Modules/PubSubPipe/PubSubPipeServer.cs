using Meta.Lib.Modules.Pipe;
using Meta.Lib.Modules.PubSub;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO.Pipes;

namespace Meta.Lib.Modules.PubSubPipe
{
    public class PubSubPipeServer : IPubSubPipeServer, IDisposable
    {
        readonly IMetaPubSub _pubSub;
        readonly ILogger _logger;
        readonly MetaPipeServer _pipeServer;
        readonly ConcurrentDictionary<MetaPipeConnection, RemoteClientConnection> _connections =
            new ConcurrentDictionary<MetaPipeConnection, RemoteClientConnection>();

        public PubSubPipeServer(string pipeName, IMetaPubSub pubSub, ILogger logger = null)
        {
            _pubSub = pubSub;
            _logger = logger;
            _pipeServer = new MetaPipeServer(pipeName, logger);
            _pipeServer.ClientConnected += PipeServer_ClientConnected;
        }

        public void Dispose()
        {
            foreach (var item in _connections)
            {
                item.Key.Disconnected -= PipeConnection_Disconnected;
                item.Value.Dispose();
            }
            _connections.Clear();

            _pipeServer.ClientConnected -= PipeServer_ClientConnected;
            _pipeServer.Dispose();
        }

        public void StartServer(Func<NamedPipeServerStream> configure = null)
        {
            _pipeServer.Start(configure);
        }

        public void StopServer()
        {
            _pipeServer.Stop();
        }

        void PipeServer_ClientConnected(object sender, PipeConnectionEventArgs e)
        {
            var connection = new RemoteClientConnection(e.Connection, _pubSub, _logger);
            if (_connections.TryAdd(e.Connection, connection))
            {
                e.Connection.Disconnected += PipeConnection_Disconnected;
            }
        }

        void PipeConnection_Disconnected(object sender, EventArgs e)
        {
            var pipeConnection = (MetaPipeConnection)sender;
            if (_connections.TryRemove(pipeConnection, out RemoteClientConnection removed))
            {
                pipeConnection.Disconnected -= PipeConnection_Disconnected;
                removed.Dispose();
            }
        }
    }
}
