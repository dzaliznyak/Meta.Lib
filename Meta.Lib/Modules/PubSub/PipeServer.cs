using Meta.Lib.Modules.Logger;
using Meta.Lib.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class PipeServer : LogWriterBase
    {
        readonly ConcurrentDictionary<Type, Type> _subscribedTypes =
            new ConcurrentDictionary<Type, Type>();

        readonly ConcurrentDictionary<string, PipeTransmit> _transmits =
            new ConcurrentDictionary<string, PipeTransmit>();

        readonly Action<Type, PipeServer> _onNewPipeSubscriber;
        readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);

        NamedPipeServerStream _server;
        StreamReader _reader;
        StreamWriter _writer;

        public bool IsConnected => _server?.IsConnected ?? false;

        public event EventHandler<EventArgs> Connected;
        public event EventHandler<EventArgs> Disconnected;

        public PipeServer(IMetaLogger log, Action<Type, PipeServer> onNewPipeSubscriber)
            : base(nameof(PipeServer), log)
        {
            _onNewPipeSubscriber = onNewPipeSubscriber;
        }

        internal void Start(string pipeName)
        {
            if (_server != null)
                throw new InvalidOperationException("Pipe server already started");

            Task.Run(async () =>
            {
                _server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 32,
                    PipeTransmissionMode.Message, PipeOptions.Asynchronous, 1024, 1024);

                await _server.WaitForConnectionAsync();

                _reader = new StreamReader(_server);
                _writer = new StreamWriter(_server)
                {
                    AutoFlush = true
                };

                Connected?.Invoke(this, EventArgs.Empty);
                WriteDebugLine($"Server: client connected {DateTime.Now:HH:mm:ss.fff}");

                while (true)
                {
                    var message = await _reader.ReadLineAsync();

                    if (message != null)
                    {
                        ProcessMessage(message);
                    }
                    else
                    {
                        Disconnect();
                        break;
                    }
                }
            });
        }

        //todo - sunchronization
        private void Disconnect()
        {
            _writer?.Dispose();
            _reader?.Dispose();
            _server.Dispose();
            _reader = null;
            _writer = null;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        internal Task<bool> SendMessage(IPubSubMessage message)
        {
            if (IsShouldSend(message))
            {
                if (!IsConnected)
                    throw new InvalidOperationException("Pipe is not connected");

                var transmit = new PipeTransmit(message);
                _transmits.TryAdd(transmit.Id, transmit);

                SendTransmit(transmit);

                return transmit.Tcs.Task;
            }

            return Task.FromResult(false);
        }

        bool IsShouldSend(IPubSubMessage message)
        {
            return _subscribedTypes.TryGetValue(message.GetType(), out Type _);
        }

        void SendTransmit(PipeTransmit transmit)
        {
            Task.Run(async () =>
            {
                try
                {
                    try
                    {
                        await _writeSemaphore.WaitAsync();
                        await _writer.WriteLineAsync(transmit.Packet);
                    }
                    finally
                    {
                        _writeSemaphore.Release();
                    }

                    await transmit.Tcs.Task.TimeoutAfter(transmit.Timeout);
                }
                catch (Exception ex)
                {
                    if (_transmits.TryRemove(transmit.Id, out var removed))
                        removed.Tcs.SetException(ex);
                }
            });
        }

        void ProcessMessage(string message)
        {
            var parts = message.Split('\t');
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
            else if (parts[0] == "r") // message response 
            {
                OnMessageResponse(parts);
            }
        }

        void OnMessageResponse(string[] parts)
        {
            string id = parts[1];

            if (_transmits.TryRemove(id, out var transmit))
            {
                string result = parts[2];
                if (result == "ok")
                {
                    transmit.Tcs.SetResult(true);
                }
                else
                {
                    var exception = JsonConvert.DeserializeObject<AggregateException>(parts[3]);
                    transmit.Tcs.SetException(exception);
                }
            }
        }
    }
}