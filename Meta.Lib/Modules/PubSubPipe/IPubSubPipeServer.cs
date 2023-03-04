using System;
using System.IO.Pipes;

namespace Meta.Lib.Modules.PubSubPipe
{
    public interface IPubSubPipeServer
    {
        void StartServer(Func<NamedPipeServerStream> configure = null);

        void StopServer();
    }
}
