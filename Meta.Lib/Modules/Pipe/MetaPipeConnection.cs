using Meta.Lib.Modules.StateMachine;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.Pipe
{
    public enum ConnectionType { None, Server, Client }

    public class MetaPipeConnection : IDisposable
    {
        const int HeaderSize = 4;
        static int ConnectionNo = 0;

        enum State { NotConnected, Connected, Disconnected, Reconnecting, Disposed }
        enum Input { Connect, Disconnect, Reconnect, Dispose }

        readonly ILogger _logger;
        readonly ConcurrentStateMachine<State, Input> _sm;
        readonly ConnectionType _connectionType;
        readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        readonly byte[] _sendHeaderBuf = new byte[HeaderSize];
        readonly string _serverName;
        readonly string _pipeName;
        readonly int _timeout;
        readonly bool _autoreconnect;

        PipeStream _pipe;
        CancellationTokenSource _cts;
        long _bytesReceived;
        long _bytesSent;

        public event EventHandler<PipeConnectionMessageEventArgs> MessageReceived;
        public event EventHandler Connected;
        public event EventHandler Disconnected;

        public bool IsConnected => _pipe?.IsConnected ?? false;
        public ConnectionType ConnectionType => _connectionType;
        public long BytesReceived => _bytesReceived;
        public long BytesSent => _bytesSent;

        public MetaPipeConnection(string pipeName, ILogger logger = null, int timeout = 1000, bool autoreconnect = true, string serverName = ".")
            : this(logger, State.NotConnected, $"ClientPipe_{pipeName}_{Interlocked.Increment(ref ConnectionNo)}")
        {
            _connectionType = ConnectionType.Client;
            _pipeName = pipeName;
            _timeout = timeout;
            _autoreconnect = autoreconnect;
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
                Debug.Assert(_pipe == null, "The pipe is already connected or connecting");
                Debug.Assert(_cts == null, "The pipe is already connected or connecting");

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

                    int expectedLength = ParseHeader(header);
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
                    FireMessageReceivedEvent(buffer.AsMemory(0, expectedLength));
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

                if (_autoreconnect)
                    Reconnect();
            }
        }

        void FireMessageReceivedEvent(ReadOnlyMemory<byte> message)
        {
            try
            {
                MessageReceived?.Invoke(this, new PipeConnectionMessageEventArgs(message.ToArray()));
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "Unhandled exception in MessageReceived event handler");
            }
        }

        static int ParseHeader(ReadOnlySpan<byte> buf)
        {
            return Unsafe.ReadUnaligned<int>(ref Unsafe.AsRef(buf[0]));
        }

        void Reconnect()
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

        public Task Send(string message)
        {
            var buf = Encoding.UTF8.GetBytes(message);
            return Send(buf);
        }

        public async Task Send(byte[] buf)
        {
            if (_sm.State == State.Disposed) throw new ObjectDisposedException(nameof(MetaPipeConnection));

            try
            {
                if (_cts == null || _cts.IsCancellationRequested)
                    throw new InvalidOperationException("Pipe is closed");

                await _writeLock.WaitAsync();

                Unsafe.WriteUnaligned(ref _sendHeaderBuf[0], buf.Length);

                await _pipe.WriteAsync(_sendHeaderBuf, 0, HeaderSize);
                await _pipe.WriteAsync(buf, 0, buf.Length);

                _bytesSent += buf.Length;
            }
            finally
            {
                _writeLock.Release();
            }
        }


    }
}
