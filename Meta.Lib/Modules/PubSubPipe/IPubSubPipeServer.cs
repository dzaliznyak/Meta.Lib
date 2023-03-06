using System;
using System.IO.Pipes;

namespace Meta.Lib.Modules.PubSubPipe
{
    public interface IPubSubPipeServer
    {
        /// <summary>
        /// Starts accepting for incoming client connections.
        /// </summary>
        /// <param name="configure">Delegate wich can be used to create NamedPipeServerStream with non-default parameters.</param>
        void StartServer(Func<NamedPipeServerStream> configure = null);

        /// <summary>
        /// Disconnects all client connections and stops accepting new connections.
        /// </summary>
        void StopServer();
    }
}
