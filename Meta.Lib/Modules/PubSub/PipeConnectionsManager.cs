using Meta.Lib.Modules.Logger;
using Meta.Lib.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public class PipeConnectionsManager : LogWriterBase
    {
        readonly Action<Type, PipeServer> _onNewPipeSubscriber;

        ImmutableList<PipeServer> _servers = ImmutableList<PipeServer>.Empty;

        internal PipeConnectionsManager(IMetaLogger log, Action<Type, PipeServer> onNewPipeSubscriber)
            : base(nameof(PipeConnectionsManager), log)
        {
            _onNewPipeSubscriber = onNewPipeSubscriber;
        }

        internal void Start(string pipeName)
        {
            var server = new PipeServer(_log, _onNewPipeSubscriber);

            server.Connected += (s, a) => 
            {
                // start waiting for the next connection
                Start(pipeName);
            };

            server.Disconnected += (s, a) =>
            {
                _servers = _servers.Remove(server);
            };

            server.Start(pipeName);

            _servers = _servers.Add(server);
        }

        // Sends a message to the clients
        internal async Task<bool> Put(IPubSubMessage message)
        {
            bool delivered = false;
            List<Exception> exceptions = null;

            foreach (var server in _servers.Where(s => s.IsConnected))
            {
                try
                {
                    if (await server.SendMessage(message))
                        delivered = true;
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
