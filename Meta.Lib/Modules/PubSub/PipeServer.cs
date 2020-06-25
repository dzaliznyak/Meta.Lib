using Meta.Lib.Modules.Logger;
using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class PipeServer : PipeConnection
    {
        readonly ConcurrentDictionary<Type, Type> _subscribedTypes =
            new ConcurrentDictionary<Type, Type>();

        readonly Action<Type, PipeServer> _onNewPipeSubscriber;
        readonly CancellationTokenSource _cts = new CancellationTokenSource();


        public PipeServer(MessageHub hub, IMetaLogger logger, Action<Type, PipeServer> onNewPipeSubscriber)
            : base(hub, nameof(PipeServer), logger)
        {
            _onNewPipeSubscriber = onNewPipeSubscriber;
        }

        internal async Task Start(string pipeName)
        {
            if (_pipe != null)
                throw new InvalidOperationException("Pipe server already started");

            var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 32,
                PipeTransmissionMode.Message, PipeOptions.Asynchronous, 1024, 1024);

            try
            {
                await pipe.WaitForConnectionAsync(_cts.Token);
            }
            catch (Exception)
            {
                pipe.Dispose();
                throw;
            }

            Init(pipe);

            FireConnectedEvent();

            WriteDebugLine($"Client connected {DateTime.Now:HH:mm:ss.fff}");

            StartReadLoop();
        }

        internal void Stop()
        {
            _cts.Cancel();

            if (_pipe == null)
                return;

            Disconnect();
        }

        internal bool IsShouldSend(IPubSubMessage message)
        {
            return _subscribedTypes.TryGetValue(message.GetType(), out Type _);
        }

        protected override async Task OnSubscribe(string[] parts)
        {
            string id = parts[1];
            Type type = Type.GetType(parts[2]);
            if (_subscribedTypes.TryAdd(type, type))
            {
                _onNewPipeSubscriber(type, this);
                WriteDebugLine($"subscribed to {type}");
            }
            await SendOkResponse(id);
        }

        protected override async Task OnUnsubscribe(string[] parts)
        {
            string id = parts[1];
            Type type = Type.GetType(parts[2]);
            _subscribedTypes.TryRemove(type, out var _);
            await SendOkResponse(id);
        }

    }
}