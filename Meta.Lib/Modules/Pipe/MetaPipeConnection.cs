using Meta.Lib.Exceptions;
using Meta.Lib.Modules.StateMachine;
using Meta.Lib.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.Pipe
{
    public enum ConnectionType { None, Server, Client }
    public enum PayloadType : byte { None, ByteArray, String, Object, Error }

    public class MetaPipeConnection : IDisposable
    {
        const int HeaderSize = 24;
        static int ConnectionNo = 0;

        enum State { NotConnected, Connected, Disconnected, Reconnecting, Disposed }
        enum Input { Connect, Disconnect, Reconnect, Dispose }

        readonly ILogger _logger;
        readonly ConcurrentStateMachine<State, Input> _sm;
        readonly ConnectionType _connectionType;
        readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        readonly MemoryStream _sendStream = new MemoryStream(256);
        readonly string _serverName;
        readonly string _pipeName;
        readonly int _timeout;
        readonly bool _autoReconnect;
        readonly ConcurrentDictionary<Guid, IResponseAwaiter> _responseAwaiters =
            new ConcurrentDictionary<Guid, IResponseAwaiter>();

        PipeStream _pipe;
        CancellationTokenSource _cts;
        long _bytesReceived;
        long _bytesSent;

        public event EventHandler<PipeMessageEventArgs> MessageReceived;
        public event EventHandler Connected;
        public event EventHandler Disconnected;

        public bool IsConnected => _pipe?.IsConnected ?? false;
        public ConnectionType ConnectionType => _connectionType;
        public long BytesReceived => _bytesReceived;
        public long BytesSent => _bytesSent;

        public MetaPipeConnection(string pipeName, ILogger logger = null, int timeout = 1000, bool autoReconnect = true, string serverName = ".")
            : this(logger, State.NotConnected, $"ClientPipe_{pipeName}_{Interlocked.Increment(ref ConnectionNo)}")
        {
            _connectionType = ConnectionType.Client;
            _pipeName = pipeName;
            _timeout = timeout;
            _autoReconnect = autoReconnect;
            _serverName = serverName;
        }

        internal MetaPipeConnection(string pipeName, NamedPipeServerStream pipe, ILogger logger)
            : this(logger, State.Connected, $"ServerPipe_{pipeName}_{Interlocked.Increment(ref ConnectionNo)}")
        {
            _connectionType = ConnectionType.Server;
            _pipe = pipe;
            _cts = new CancellationTokenSource();
        }

        MetaPipeConnection(ILogger logger, State initialState, string name)
        {
            _logger = logger;

            var builder = new StateMachineBuilder<State, Input>(_logger);

            builder
                .OnEntry(State.Connected).Do(FireConnectedEvent)
                .OnExit(State.Connected).Do(FireDisconnectedEvent);

            builder.On(Input.Connect)
                .In(State.NotConnected).DoAsync(ConnectInternal).Set(State.Connected)
                .In(State.Disconnected).DoAsync(ConnectInternal).Set(State.Connected)
                .In(State.Reconnecting).DoAsync(ConnectInternal).Set(State.Connected)
                .In(State.Connected).Do(() => throw new InvalidOperationException("Pipe already connected"));

            builder.On(Input.Disconnect)
                .Do(DisconnectInternal)
                .Set(State.Disconnected);

            builder.On(Input.Reconnect)
                .In(State.NotConnected).Do(ReconnectInternal).Set(State.Reconnecting)
                .In(State.Disconnected).Do(ReconnectInternal).Set(State.Reconnecting)
                .In(State.Connected).Do(() => throw new InvalidOperationException("Pipe already connected"));

            builder.On(Input.Dispose)
                .Do(DisposeInternal)
                .Set(State.Disposed);

            _sm = builder.WithInitialState(initialState).Build(name);
        }

        public void Dispose()
        {
            if (_sm.State != State.Disposed)
            {
                _sm.Fire(Input.Dispose);
                _sm.Dispose();
            }
        }

        void DisposeInternal()
        {
            if (_sm.State != State.Disposed)
            {
                DisconnectInternal();

                _writeLock.Wait();
                _writeLock.Dispose();
            }
        }

        public Task Connect()
        {
            return _sm.FireAsync(Input.Connect);
        }

        async Task ConnectInternal()
        {
            NamedPipeClientStream pipe = null;

            try
            {
                Debug.Assert(_pipe == null || _cts == null, "The pipe is already connected or connecting");

                _cts = new CancellationTokenSource();
                pipe = new NamedPipeClientStream(_serverName, _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

                await pipe.ConnectAsync(_timeout, _cts.Token);

                _pipe = pipe;

                StartReadPipe();
            }
            catch (Exception)
            {
                pipe?.Dispose();
                _pipe = null;

                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;

                throw;
            }
        }

        public void Reconnect()
        {
            _sm.Fire(Input.Reconnect);
        }

        void ReconnectInternal()
        {
            Debug.Assert(_cts == null);
            Task.Run(() => ReconnectionLoop());
        }

        async Task ReconnectionLoop()
        {
            do
            {
                try
                {
                    await Connect();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogInformation(ex, "Reconnection error");
                }
            }
            while (!IsConnected && _sm.State == State.Reconnecting);
        }

        public void Disconnect()
        {
            _sm.Fire(Input.Disconnect);
        }

        void DisconnectInternal()
        {
            try
            {
                try
                {
                    if (IsConnected)
                        _pipe.WaitForPipeDrain();
                }
                catch (Exception)
                {
                }

                if (_cts != null)
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = null;
                }

                if (_pipe != null)
                {
                    _pipe.Dispose();
                    _pipe = null;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "Exception in DisconnectInternal");
            }
        }

        void FireConnectedEvent()
        {
            try
            {
                Connected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "Unhandled exception in Connected event handler");
            }
        }

        void FireDisconnectedEvent()
        {
            try
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "Unhandled exception in Disconnected event handler");
            }
        }

        internal void StartReadPipe()
        {
            Task.Run(() => ReadingPipeLoop(_pipe, _cts));
        }

        async Task ReadingPipeLoop(PipeStream pipe, CancellationTokenSource cts)
        {
            var header = new byte[HeaderSize];
            var buffer = new byte[4096];

            while (!cts.IsCancellationRequested && pipe.IsConnected)
            {
                try
                {
                    // read header
                    var readLen = await pipe.ReadAsync(header, 0, HeaderSize, cts.Token);
                    if (readLen == 0)
                        break;

                    int expectedLength = GetDataLength(header);
                    if (buffer.Length < expectedLength)
                    {
                        int newSize = (expectedLength / 4096 + 1) * 4096;
                        Array.Resize(ref buffer, newSize);
                    }

                    // read data
                    readLen = await pipe.ReadAsync(buffer, 0, expectedLength, cts.Token);
                    if (readLen == 0)
                        break;
                    else if (readLen != expectedLength)
                        throw new Exception("Read byte count not equal to expected");

                    _bytesReceived += readLen;

                    ProcessReceivedMessage(header, buffer, expectedLength);
                }
                catch (Exception ex)
                {
                    if (!cts.IsCancellationRequested)
                    {
                        _logger?.LogError(ex, "Reading pipe error");
                    }
                    break;
                }
            }

            if (!cts.IsCancellationRequested)
            {
                Disconnect();

                if (_autoReconnect)
                    Reconnect();
            }
        }

        void ProcessReceivedMessage(ReadOnlySpan<byte> header, byte[] buffer, int dataLength)
        {
            try
            {
                var flags = Unsafe.ReadUnaligned<byte>(ref Unsafe.AsRef(header[4]));
                var payloadType = (PayloadType)Unsafe.ReadUnaligned<byte>(ref Unsafe.AsRef(header[5]));
                var typeNameLength = Unsafe.ReadUnaligned<short>(ref Unsafe.AsRef(header[6]));
                var correlationId = Unsafe.ReadUnaligned<Guid>(ref Unsafe.AsRef(header[8]));

                if (payloadType == PayloadType.Object && typeNameLength > 0)
                {
                    var span = new ReadOnlySpan<byte>(buffer);
                    var typeNameBuf = span.Slice(0, typeNameLength);
                    string typeName = JsonSerializer.Deserialize<string>(typeNameBuf);
                    Type objType = Type.GetType(typeName);

                    var payloadBuf = span.Slice(typeNameLength, dataLength - typeNameLength);
                    var payload = JsonSerializer.Deserialize(payloadBuf, objType);

                    CompleteAwaiter(correlationId, payload);

                    FireMessageReceivedEvent(new PipeMessageEventArgs(payload) { CorrelationId = correlationId });
                }
                else if (payloadType == PayloadType.String)
                {
                    var str = Encoding.UTF8.GetString(buffer, 0, dataLength);

                    CompleteAwaiter(correlationId, str);

                    FireMessageReceivedEvent(new PipeMessageEventArgs(str) { CorrelationId = correlationId });
                }
                else if (payloadType == PayloadType.ByteArray)
                {
                    var data = new byte[dataLength];
                    Array.Copy(buffer, 0, data, 0, dataLength);

                    CompleteAwaiter(correlationId, data);

                    FireMessageReceivedEvent(new PipeMessageEventArgs(data) { CorrelationId = correlationId });
                }
                else if (payloadType == PayloadType.Error)
                {
                    var span = new ReadOnlySpan<byte>(buffer);
                    var dataBuf = span.Slice(0, dataLength);
                    var error = JsonSerializer.Deserialize<ErrorDescription>(dataBuf);

                    CompleteAwaiterWithError(correlationId, error);

                    FireMessageReceivedEvent(new PipeMessageEventArgs(error) { CorrelationId = correlationId });
                }
                else
                {
                    Debug.Assert(false);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "Failed to process received message");
            }
        }

        void FireMessageReceivedEvent(PipeMessageEventArgs args)
        {
            try
            {
                MessageReceived?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "Unhandled exception in MessageReceived event handler");
            }
        }

        static int GetDataLength(ReadOnlySpan<byte> buffer)
        {
            return Unsafe.ReadUnaligned<int>(ref Unsafe.AsRef(buffer[0]));
        }

        public Task Send(string message, Guid correlationId = default)
        {
            if (_sm.State == State.Disposed) throw new ObjectDisposedException(nameof(MetaPipeConnection));
            if (_cts == null || _cts.IsCancellationRequested) throw new InvalidOperationException("Pipe is closed");

            var buf = Encoding.UTF8.GetBytes(message);
            return SendAsBuffer(buf, correlationId, PayloadType.String);
        }

        public Task Send(byte[] buf, Guid correlationId = default)
        {
            if (_sm.State == State.Disposed) throw new ObjectDisposedException(nameof(MetaPipeConnection));
            if (_cts == null || _cts.IsCancellationRequested) throw new InvalidOperationException("Pipe is closed");

            return SendAsBuffer(buf, correlationId, PayloadType.ByteArray);
        }

        async Task SendAsBuffer(byte[] buf, Guid correlationId, PayloadType payloadType)
        {
            try
            {
                await _writeLock.WaitAsync();

                _sendStream.Position = HeaderSize;
                _sendStream.Write(buf, 0, buf.Length);

                var sendBuf = _sendStream.GetBuffer();

                // header
                Unsafe.WriteUnaligned(ref sendBuf[0], buf.Length); //DataLength
                Unsafe.WriteUnaligned(ref sendBuf[4], (byte)0x00); // Flags
                Unsafe.WriteUnaligned(ref sendBuf[5], (byte)payloadType); // PayloadType
                Unsafe.WriteUnaligned(ref sendBuf[6], (short)0); // TypeNameLength
                Unsafe.WriteUnaligned(ref sendBuf[8], correlationId); // CorrelationId

                await _pipe.WriteAsync(sendBuf, 0, (int)_sendStream.Position);

                _bytesSent += (int)_sendStream.Position;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        // HEADER (24b len)  [DataLength - 4b] [Flags - 1b] [PayloadType - 1b] [TypeNameLength - 2b] [CorrelationId - 16b]
        public async Task Send<T>(T obj, Guid correlationId = default)
        {
            if (_sm.State == State.Disposed) throw new ObjectDisposedException(nameof(MetaPipeConnection));
            if (_cts == null || _cts.IsCancellationRequested) throw new InvalidOperationException("Pipe is closed");

            try
            {
                await _writeLock.WaitAsync();

                _sendStream.Position = HeaderSize;

                // type name serialization
                long prevPosition = _sendStream.Position;
                JsonSerializer.Serialize(_sendStream, typeof(T).AssemblyQualifiedName);
                short typeNameLength = (short)(_sendStream.Position - prevPosition);

                // object serialization
                JsonSerializer.Serialize(_sendStream, obj);

                // header
                var buf = _sendStream.GetBuffer();
                Unsafe.WriteUnaligned(ref buf[0], (int)_sendStream.Position - HeaderSize); //DataLength
                Unsafe.WriteUnaligned(ref buf[4], (byte)0x00); // Flags
                Unsafe.WriteUnaligned(ref buf[5], (byte)PayloadType.Object); // PayloadType
                Unsafe.WriteUnaligned(ref buf[6], typeNameLength); // TypeNameLength
                Unsafe.WriteUnaligned(ref buf[8], correlationId); // CorrelationId

                await _pipe.WriteAsync(buf, 0, (int)_sendStream.Position);

                _bytesSent += (int)_sendStream.Position;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task Send(Type type, object obj, Guid correlationId = default)
        {
            if (_sm.State == State.Disposed) throw new ObjectDisposedException(nameof(MetaPipeConnection));
            if (_cts == null || _cts.IsCancellationRequested) throw new InvalidOperationException("Pipe is closed");

            try
            {
                await _writeLock.WaitAsync();

                _sendStream.Position = HeaderSize;

                // type name serialization
                long prevPosition = _sendStream.Position;
                JsonSerializer.Serialize(_sendStream, type.AssemblyQualifiedName);
                short typeNameLength = (short)(_sendStream.Position - prevPosition);

                // object serialization
                JsonSerializer.Serialize(_sendStream, obj, type);

                // header
                var buf = _sendStream.GetBuffer();
                Unsafe.WriteUnaligned(ref buf[0], (int)_sendStream.Position - HeaderSize); //DataLength
                Unsafe.WriteUnaligned(ref buf[4], (byte)0x00); // Flags
                Unsafe.WriteUnaligned(ref buf[5], (byte)PayloadType.Object); // PayloadType
                Unsafe.WriteUnaligned(ref buf[6], typeNameLength); // TypeNameLength
                Unsafe.WriteUnaligned(ref buf[8], correlationId); // CorrelationId

                await _pipe.WriteAsync(buf, 0, (int)_sendStream.Position);

                _bytesSent += (int)_sendStream.Position;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task Send(ErrorDescription error, Guid correlationId)
        {
            if (_sm.State == State.Disposed) throw new ObjectDisposedException(nameof(MetaPipeConnection));
            if (_cts == null || _cts.IsCancellationRequested) throw new InvalidOperationException("Pipe is closed");

            try
            {
                await _writeLock.WaitAsync();

                _sendStream.Position = HeaderSize;

                // error description serialization
                JsonSerializer.Serialize(_sendStream, error);

                // header
                var buf = _sendStream.GetBuffer();
                Unsafe.WriteUnaligned(ref buf[0], (int)_sendStream.Position - HeaderSize); //DataLength
                Unsafe.WriteUnaligned(ref buf[4], (byte)0x00); // Flags
                Unsafe.WriteUnaligned(ref buf[5], (byte)PayloadType.Error); // PayloadType
                Unsafe.WriteUnaligned(ref buf[6], (short)0); // TypeNameLength
                Unsafe.WriteUnaligned(ref buf[8], correlationId); // CorrelationId

                await _pipe.WriteAsync(buf, 0, (int)_sendStream.Position);

                _bytesSent += (int)_sendStream.Position;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<TResp> SendAndWaitResponse<T, TResp>(T obj, Guid correlationId, int timeout = 5000, CancellationToken cancellationToken = default)
        {
            if (_sm.State == State.Disposed) throw new ObjectDisposedException(nameof(MetaPipeConnection));
            if (_cts == null || _cts.IsCancellationRequested) throw new InvalidOperationException("Pipe is closed");

            var awaiter = new ResponseAwaiter<TResp>();
            if (!_responseAwaiters.TryAdd(correlationId, awaiter))
                throw new InvalidOperationException("Request with the same correlation ID is already running");

            await Send(obj, correlationId);

            return await awaiter.Tcs.Task.TimeoutAfter(timeout, cancellationToken);
        }

        public async Task<TResp> SendAndWaitResponse<TResp>(Type type, object obj, Guid correlationId, int timeout = 5000, CancellationToken cancellationToken = default)
        {
            if (_sm.State == State.Disposed) throw new ObjectDisposedException(nameof(MetaPipeConnection));
            if (_cts == null || _cts.IsCancellationRequested) throw new InvalidOperationException("Pipe is closed");

            var awaiter = new ResponseAwaiter<TResp>();
            if (!_responseAwaiters.TryAdd(correlationId, awaiter))
                throw new InvalidOperationException("Request with the same correlation ID is already running");

            await Send(type, obj, correlationId);

            return await awaiter.Tcs.Task.TimeoutAfter(timeout, cancellationToken);
        }

        void CompleteAwaiter(Guid correlationId, object result)
        {
            if (_responseAwaiters.TryRemove(correlationId, out var awaiter))
            {
                awaiter.SetResult(result);
            }
        }

        void CompleteAwaiterWithError(Guid correlationId, ErrorDescription error)
        {
            if (_responseAwaiters.TryRemove(correlationId, out var awaiter))
            {
                awaiter.SetException(new RemoteException(error));
            }
        }

    }
}
