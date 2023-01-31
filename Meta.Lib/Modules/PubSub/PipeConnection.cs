using Meta.Lib.Utils;
using Microsoft.Extensions.Logging;
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

    internal class PipeConnection
    {
        static int InstanceNo = 0;

        readonly object _lock = new object();

        protected readonly ILogger _logger;
        protected readonly MessageHub _hub;

        PipeStream _pipe;
        StreamReader _reader;
        StreamWriter _writer;

        readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
        readonly ConcurrentDictionary<string, PipeTransmit> _transmits =
            new ConcurrentDictionary<string, PipeTransmit>();

        internal event EventHandler<EventArgs> Connected;
        internal event EventHandler<EventArgs> Disconnected;

        internal string Id { get; } = (++InstanceNo).ToString();

        internal bool IsConnected => _pipe?.IsConnected ?? false;
        internal bool IsStarted => _pipe != null;


        public PipeConnection(MessageHub hub, ILogger logger)
        {
            _logger = logger;
            _hub = hub;
        }

        protected void InitPipeConnection(PipeStream pipe)
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
                    _logger?.LogError(ex, "Failed to start pipe read loop");
                    OnDisconnected();
                }
            });
        }

        internal virtual Task Disconnect()
        {
            lock (_lock)
            {
                try { _pipe?.WaitForPipeDrain(); } catch (Exception) { }
                try { _pipe?.Dispose(); } catch (Exception) { }
                _pipe = null;
            }
            return Task.CompletedTask;
        }

        void OnDisconnected()
        {
            _logger?.LogInformation("Pipe disconnected");

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
                    _logger?.LogInformation("Cancelled transmit '{Packet}'", removed.Packet);
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
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Pipe connection failed to process a packet");
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
                _logger?.LogDebug(ex, "Exception while processing the message");
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
                            (aggException.InnerException is NoSubscribersException ||
                             aggException.InnerException is TimeoutException))
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

        internal Task<bool> SendMessage(string message, PipeMessageType pipeMessageType, int millisecondsTimeout)
        {
            if (!IsConnected)
                throw new Exception("Pipe is not connected");

            var transmit = new PipeTransmit(message, pipeMessageType, millisecondsTimeout);
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
                    _logger?.LogError("Send transmit error: '{Message}, packet: {Packet}'", ex.Message, transmit.Packet);
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
