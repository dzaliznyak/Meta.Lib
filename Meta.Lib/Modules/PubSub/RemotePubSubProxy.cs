using Meta.Lib.Modules.Logger;
using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class RemotePubSubProxy : PipeConnection
    {
        readonly string _pipeName;
        readonly string _serverName;
        readonly ConcurrentDictionary<Type, string> _subscribedTypes = 
            new ConcurrentDictionary<Type, string>();


        public RemotePubSubProxy(MessageHub hub, IMetaLogger logger, string pipeName, string serverName = ".")
            :base(hub, nameof(RemotePubSubProxy), logger)
        {
            _pipeName = pipeName;
            _serverName = serverName;
        }

        internal async Task Connect(int millisecondsTimeout = 5_000)
        {
            var pipe = new NamedPipeClientStream(_serverName, _pipeName,
                PipeDirection.InOut, PipeOptions.Asynchronous);

            await pipe.ConnectAsync(millisecondsTimeout);

            Init(pipe);

            await ResubscribeAllMessages();

            FireConnectedEvent();

            StartReadLoop();
        }

        async Task ResubscribeAllMessages()
        {
            foreach (var item in _subscribedTypes.Values)
                await WriteToPipe($"s\t{item}");
        }

        public async Task Subscribe<TMessage>()
        {
            var str = $"s\t{typeof(TMessage).AssemblyQualifiedName}";
            await WriteToPipe(str);
            _subscribedTypes.TryAdd(typeof(TMessage), typeof(TMessage).AssemblyQualifiedName);
        }

        public async Task Unsubscribe(Type type)
        {
            var str = $"u\t{type.AssemblyQualifiedName}";
            await WriteToPipe(str);
            _subscribedTypes.TryRemove(type, out var _);
        }

    }
}
