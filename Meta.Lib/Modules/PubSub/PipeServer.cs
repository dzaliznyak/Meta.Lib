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
            : base(hub, logger)
        {
            _onNewPipeSubscriber = onNewPipeSubscriber;
        }

        internal async Task Start(string pipeName, Func<NamedPipeServerStream> configure)
        {
            if (IsStarted)
                throw new InvalidOperationException("Pipe server already started");

            NamedPipeServerStream pipe = configure?.Invoke();

            if (pipe == null)
            {
                pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 32,
                        PipeTransmissionMode.Message, PipeOptions.Asynchronous, 4096, 4096);
            }

            try
            {
                await pipe.WaitForConnectionAsync(_cts.Token);
            }
            catch (Exception)
            {
                pipe.Dispose();
                throw;
            }

            InitPipeConnection(pipe);

            StartReadLoop();

            FireConnectedEvent();
        }

        internal void Stop()
        {
            _cts.Cancel();
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
                _logger.Trace($"Client subscribed to {type}");
            }
            await SendOkResponse(id);
        }

        protected override async Task OnUnsubscribe(string[] parts)
        {
            string id = parts[1];
            Type type = Type.GetType(parts[2]);
            _subscribedTypes.TryRemove(type, out var _);
            _logger.Trace($"Client unsubscribed from {type}");
            await SendOkResponse(id);
        }

    }
}