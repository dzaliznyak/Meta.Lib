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
            :base(hub, logger)
        {
            _pipeName = pipeName;
            _serverName = serverName;
        }

        internal async Task Connect(int millisecondsTimeout = 5_000)
        {
            var pipe = new NamedPipeClientStream(_serverName, _pipeName,
                PipeDirection.InOut, PipeOptions.Asynchronous);

            try
            {
                await pipe.ConnectAsync(millisecondsTimeout);
            }
            catch (Exception)
            {
                pipe.Dispose();
                throw;
            }

            Init(pipe);
            
            StartReadLoop();

            await ResubscribeAllMessages();

            FireConnectedEvent();
        }

        async Task ResubscribeAllMessages()
        {
            foreach (var item in _subscribedTypes.Values)
                await SendMessage(item, PipeMessageType.Subscribe);
        }

        public async Task Subscribe<TMessage>()
        {
            var type = typeof(TMessage);
            await SendMessage(type.AssemblyQualifiedName, PipeMessageType.Subscribe);
            _subscribedTypes.TryAdd(type, type.AssemblyQualifiedName);
        }

        public async Task Unsubscribe(Type type)
        {
            await SendMessage(type.AssemblyQualifiedName, PipeMessageType.Unsubscribe);
            _subscribedTypes.TryRemove(type, out var _);
        }

    }
}
