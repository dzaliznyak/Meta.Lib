using Meta.Lib.Modules.Logger;
using Meta.Lib.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal enum PipeMessageType : byte
    {
        Subscribe = (byte)'s',
        Unsubscribe = (byte)'u',
        Message = (byte)'m',
        MessageResponse = (byte)'r'
    }

    internal class PipeConnection : LogWriterBase
    {
        static int InstanceNo = 0;

        readonly object _lock = new object();
        protected readonly MessageHub _hub;

        protected PipeStream _pipe;
        StreamReader _reader;
        StreamWriter _writer;

        protected readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
        protected readonly ConcurrentDictionary<string, PipeTransmit> _transmits =
            new ConcurrentDictionary<string, PipeTransmit>();

        public event EventHandler<EventArgs> Connected;
        public event EventHandler<EventArgs> Disconnected;

        public string Id { get; } = (++InstanceNo).ToString();

        public bool IsConnected => _pipe?.IsConnected ?? false;


        public PipeConnection(MessageHub hub, string name, IMetaLogger logger)
            : base(name, logger)
        {
            _hub = hub;
        }

        protected void Init(PipeStream pipe)
        {
            if (_pipe != null || _reader != null || _writer != null)
                throw new InvalidOperationException();

            lock (_lock)
            {
                _pipe = pipe;
                _reader = new StreamReader(pipe);
                _writer = new StreamWriter(pipe)
                {
                    AutoFlush = true
                };
            }
        }

        protected void StartReadLoop()
        {
            Task.Run(async () =>
            {
                try
                {
                    while (_reader != null)
                    {
                        var message = await _reader.ReadLineAsync();
                        //WriteDebugLine($">>> recv:  {message}");

                        if (message != null)
                        {
                            ProcessPacket(message);
                        }
                        else
                        {
                            OnDisconnected();
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteLine(ex);
                    OnDisconnected();
                }
            });
        }

        public void Disconnect()
        {
            lock (_lock)
            {
                try { _pipe?.WaitForPipeDrain(); } catch (Exception) { }
                try { _pipe?.Dispose(); } catch (Exception) { }
                _pipe = null;
            }
        }

        void OnDisconnected()
        {
            WriteDebugLine($">>>> {Id} OnDisconnected");

            lock (_lock)
            {
                try { _pipe?.WaitForPipeDrain(); } catch (Exception) { }
                try { _writer?.Dispose(); } catch (Exception) { }
                try { _reader?.Dispose(); } catch (Exception) { }
                try { _pipe?.Dispose(); } catch (Exception) { }

                _pipe = null;
                _reader = null;
                _writer = null;
            }

            ClearTransmits();

            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        void ClearTransmits()
        {
            foreach (var kvp in _transmits.Keys.ToList())
            {
                if (_transmits.TryRemove(kvp, out var removed))
                {
                    removed.Tcs.TrySetCanceled();
                }
            }
        }

        protected void FireConnectedEvent()
        {
            Connected?.Invoke(this, EventArgs.Empty);
        }

        protected void ProcessPacket(string packet)
        {
            Task.Run(async () =>
            {
                try
                {
                    var parts = packet.Split('\t');
                    if (parts[0] == "m")
                    {
                        await OnMessage(parts);
                    }
                    else if (parts[0] == "r")
                    {
                        OnMessageResponse(parts);
                    }
                    else if (parts[0] == "s")
                    {
                        await OnSubscribe(parts);
                    }
                    else if (parts[0] == "u")
                    {
                        await OnUnsubscribe(parts);
                    }
                    else
                    {
                        throw new InvalidDataException();
                    }
                }
                catch (Exception)
                {
                }
            });
        }

        protected virtual Task OnSubscribe(string[] parts)
        {
            throw new NotImplementedException();
        }

        protected virtual Task OnUnsubscribe(string[] parts)
        {
            throw new NotImplementedException();
        }

        async Task OnMessage(string[] parts)
        {
            string id = parts[1];
            Type type = Type.GetType(parts[2]);
            var message = JsonConvert.DeserializeObject(parts[3], type) as IPubSubMessage;
            message.RemoteConnectionId = this.Id;

            try
            {
                await _hub.Publish(message);
                await SendOkResponse(id);
            }
            catch (Exception ex)
            {
                WriteDebugLine(ex);
                await SendErrorResponse(id, ex);
            }
        }

        void OnMessageResponse(string[] parts)
        {
            string id = parts[1];

            if (_transmits.TryRemove(id, out var transmit))
            {
                try
                {
                    string result = parts[2];
                    if (result == "ok")
                    {
                        transmit.Tcs.SetResult(true);
                    }
                    else //if (result == "error")
                    {
                        var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
                        var aggException = JsonConvert.DeserializeObject<AggregateException>(parts[3], settings);
                        if (aggException.InnerExceptions.Count == 1 &&
                            aggException.InnerException is NoSubscribersException)
                        {
                            transmit.Tcs.SetException(aggException.InnerException);
                        }
                        else
                        {
                            transmit.Tcs.SetException(aggException);
                        }
                    }
                }
                catch (Exception ex)
                {
                    transmit.Tcs.SetException(new AggregateException("The message response has an invalid format", ex));
                }
            }
        }

        internal Task<bool> SendMessage(IPubSubMessage message, PipeMessageType pipeMessageType)
        {
            if (!IsConnected)
                throw new Exception("Pipe is not connected");

            var transmit = new PipeTransmit(message, pipeMessageType);
            _transmits.TryAdd(transmit.Id, transmit);

            SendTransmit(transmit);

            return transmit.Tcs.Task;
        }

        internal Task<bool> SendMessage(string message, PipeMessageType pipeMessageType)
        {
            if (!IsConnected)
                throw new Exception("Pipe is not connected");

            var transmit = new PipeTransmit(message, pipeMessageType);
            _transmits.TryAdd(transmit.Id, transmit);

            SendTransmit(transmit);

            return transmit.Tcs.Task;
        }

        void SendTransmit(PipeTransmit transmit)
        {
            Task.Run(async () =>
            {
                try
                {
                    await WriteToPipe(transmit.Packet);
                    await transmit.Tcs.Task.TimeoutAfter(transmit.Timeout);
                }
                catch (Exception ex)
                {
                    if (_transmits.TryRemove(transmit.Id, out var removed))
                        removed.Tcs.SetException(ex);
                }
            });
        }

        protected async Task WriteToPipe(string str)
        {
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

        protected async Task SendOkResponse(string id)
        {
            if (IsConnected)
            {
                var str = $"r\t{id}\tok";
                await WriteToPipe(str);
            }
        }

        protected async Task SendErrorResponse(string id, Exception exception)
        {
            if (IsConnected)
            {
                if (!(exception is AggregateException))
                    exception = new AggregateException(exception);

                var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
                var exceptionStr = JsonConvert.SerializeObject(exception, exception.GetType(), settings);
                var str = $"r\t{id}\terror\t{exceptionStr}";
                await WriteToPipe(str);
            }
        }

    }
}
