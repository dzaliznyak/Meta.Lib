using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public class RemotePubSubProxy
    {
        readonly IMetaPubSub _hub;
        readonly string _pipeName;
        readonly string _serverName;
        readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
        readonly ConcurrentDictionary<Type, string> _subscribedTypes = new ConcurrentDictionary<Type, string>();


        public event EventHandler<EventArgs> Connected;
        public event EventHandler<EventArgs> Disconnected;

        NamedPipeClientStream _client;
        StreamWriter _writer;
        StreamReader _reader;

        public bool IsConnected => _client?.IsConnected ?? false;

        public RemotePubSubProxy(IMetaPubSub hub, string pipeName, string serverName = ".")
        {
            _hub = hub;
            _pipeName = pipeName;
            _serverName = serverName;
        }

        //todo - Disconnect
        internal async Task Connect(int millisecondsTimeout = 5_000)
        {
            Console.WriteLine($">>> proxy connecting ...  {DateTime.Now:HH:mm:ss.fff}");

            _client = new NamedPipeClientStream(_serverName, _pipeName,
                PipeDirection.InOut, PipeOptions.Asynchronous);

            await _client.ConnectAsync(millisecondsTimeout);

            _reader = new StreamReader(_client);
            _writer = new StreamWriter(_client)
            {
                AutoFlush = true
            };

            await ResubscribeAllMessages();

            Connected?.Invoke(this, EventArgs.Empty);
            Console.WriteLine($">>> proxy connected: {DateTime.Now:HH:mm:ss.fff}");

            var t = Task.Run(async () =>
            {
                while (true)
                {
                    var temp = await _reader.ReadLineAsync();

                    if (temp == null)
                    {
                        Disconnect();
                        return;
                    }

                    //Console.WriteLine($">>> proxy recv: {temp}");

                    var parts = temp.Split('\t');
                    if (parts[0] == "m")
                    {
                        string id = parts[1];
                        Type type = Type.GetType(parts[2]);
                        var message = JsonConvert.DeserializeObject(parts[3], type) as IPubSubMessage;

                        try
                        {
                            await _hub.Publish(message);
                            await SendOkResponse(id);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"{ex.Message}");
                            await SendErrorResponse(id, ex);
                        }
                    }
                }

                //todo - catch exception and reconnect
            });
        }

        async Task ResubscribeAllMessages()
        {
            foreach (var item in _subscribedTypes.Values)
                await WriteToPipe($"s\t{item}");
        }

        //todo - synchronization
        public void Disconnect()
        {
            _writer?.Dispose();
            _reader?.Dispose();
            _client.Dispose();
            _reader = null;
            _writer = null;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        async Task WriteToPipe(string str)
        {
            if (_writer == null)
                throw new InvalidOperationException("Pipe connection is not opened");
            
            try
            {
                await _writeSemaphore.WaitAsync();
                await _writer.WriteLineAsync(str);
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        async Task SendOkResponse(string id)
        {
            var str = $"r\t{id}\tok";
            await WriteToPipe(str);
        }

        async Task SendErrorResponse(string id, Exception exception)
        {
            var exceptionStr = JsonConvert.SerializeObject(exception);
            var str = $"r\t{id}\terror\t{exceptionStr}";
            await WriteToPipe(str);
        }

        public async Task Subscribe<TMessage>()
        {
            var str = $"s\t{typeof(TMessage).AssemblyQualifiedName}";
            await WriteToPipe(str);
            _subscribedTypes.TryAdd(typeof(TMessage), typeof(TMessage).AssemblyQualifiedName);
        }

        //todo - unsubscribe

    }
}
