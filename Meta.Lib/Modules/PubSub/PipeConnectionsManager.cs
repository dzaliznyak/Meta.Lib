using Meta.Lib.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public class PipeConnectionsManager
    {
        ImmutableList<PipeServer> _servers = ImmutableList<PipeServer>.Empty;

        internal void Start(string pipeName)
        {
            var server = new PipeServer();

            server.Connected += (s, a) => 
            {
                // start waiting for the next connection
                Start(pipeName);
            };

            server.Disconnected += (s, a) =>
            {
                _servers = _servers.Remove(server);
                Console.WriteLine($"Server removed");
            };

            server.Start(pipeName);

            _servers = _servers.Add(server);
            Console.WriteLine($"Server added");
        }

        // Sends a message to the clients
        internal async Task<bool> Put(IPubSubMessage message)
        {
            Console.WriteLine($"Put: {message}");
            List<Exception> exceptions = null;

            foreach (var server in _servers.Where(s => s.IsConnected))
            {
                try
                {
                    await server.SendMessage(message);
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

            //todo - processed at least once
            return true;
        }


    }
}
