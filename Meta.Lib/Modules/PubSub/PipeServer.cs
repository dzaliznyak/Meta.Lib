using Meta.Lib.Modules.Logger;
using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class PipeServer : PipeConnection
    {
        readonly ConcurrentDictionary<Type, Type> _subscribedTypes =
            new ConcurrentDictionary<Type, Type>();

        readonly Action<Type, PipeServer> _onNewPipeSubscriber;


        public PipeServer(MessageHub hub, IMetaLogger logger, Action<Type, PipeServer> onNewPipeSubscriber)
            : base(hub, nameof(PipeServer), logger)
        {
            _onNewPipeSubscriber = onNewPipeSubscriber;
        }

        internal async Task Start(string pipeName)
        {
            if (_pipe != null)
                throw new InvalidOperationException("Pipe server already started");

            var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 32,
                PipeTransmissionMode.Message, PipeOptions.Asynchronous, 1024, 1024);

            await server.WaitForConnectionAsync();

            Init(server);

            FireConnectedEvent();

            WriteDebugLine($"Server: client connected {DateTime.Now:HH:mm:ss.fff}");

            StartReadLoop();
        }

        internal bool IsShouldSend(IPubSubMessage message)
        {
            return _subscribedTypes.TryGetValue(message.GetType(), out Type _);
        }

        protected override void ProcessPacket(string packet)
        {
            var parts = packet.Split('\t');
            if (parts[0] == "s") // subscribe
            {
                var type = Type.GetType(parts[1]);
                if (_subscribedTypes.TryAdd(type, type))
                {
                    _onNewPipeSubscriber(type, this);
                    WriteDebugLine($"subscribed to {type}");
                }
            }
            else if (parts[0] == "u") // unsubscribe
            {
                var type = Type.GetType(parts[1]);
                _subscribedTypes.TryRemove(type, out var _);
            }
            else
            {
                base.ProcessPacket(packet);
            }
        }

    }
}