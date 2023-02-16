using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.Pipe
{
    public sealed class MetaPipeServer : IDisposable
    {
        readonly List<MetaPipeConnection> _connections = new List<MetaPipeConnection>();
        readonly SemaphoreSlim _startStopLock = new SemaphoreSlim(1, 1);
        readonly ILogger _logger;

        Task _worker;
        CancellationTokenSource _cts;
        bool _disposed;


        public event EventHandler<PipeConnectionEventArgs> ClientConnected;

        public string PipeName { get; }
        public bool IsStarted => _worker != null;

        /// <summary>
        /// Unsafe property! For debug only. 
        /// </summary>
        public long TotalBytesReceived => _connections.Select(c => c.BytesReceived).Sum();

        public MetaPipeServer(string pipeName, ILogger logger = null)
        {
            PipeName = pipeName;
            _logger = logger;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                Stop();

                _startStopLock.Dispose();
            }
        }

        public void Start(Func<NamedPipeServerStream> configure = null)
        {
            try
            {
                _startStopLock.Wait();

                if (_worker != null)
                    throw new InvalidOperationException("Pipe server is already started");

                _cts = new CancellationTokenSource();

                _worker = Task.Factory.StartNew(() =>
                {
                    return WaitingForConnectionsLoop(configure, _cts);

                }, cancellationToken: default, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .Unwrap();

                _logger?.LogInformation("MetaPipeServer '{name}' started", PipeName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "MetaPipeServer '{name}' start failed", PipeName);
                throw;
            }
            finally
            {
                _startStopLock.Release();
            }
        }

        async Task WaitingForConnectionsLoop(Func<NamedPipeServerStream> configure, CancellationTokenSource cts)
        {
            while (!cts.IsCancellationRequested)
            {
                NamedPipeServerStream pipe = null;
                MetaPipeConnection connection = null;
                try
                {
                    pipe = configure?.Invoke() ??
                        new NamedPipeServerStream(PipeName, PipeDirection.InOut, 32,
                                PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096);

                    await pipe.WaitForConnectionAsync(cts.Token);

                    connection = new MetaPipeConnection(PipeName, pipe, _logger);
                    lock (_connections)
                    {
                        _connections.Add(connection);
                    }

                    connection.Disconnected += Connection_Disconnected;

                    FireClientConnectedEvent(connection);

                    connection.StartReadPipe();

                    _logger?.LogInformation("MetaPipeServer '{name}' client connected, total count: {count}",
                        PipeName, _connections.Count);
                }
                catch (Exception ex)
                {
                    if (!(ex is OperationCanceledException))
                        _logger?.LogError(ex, "WaitingForConnectionsLoop '{name}' error", PipeName);

                    if (connection != null)
                    {
                        connection.Dispose();
                        lock (_connections)
                        {
                            _connections.Remove(connection);
                        }
                    }
                    else
                    {
                        pipe?.Dispose();
                    }
                }
            }
        }

        void Connection_Disconnected(object sender, EventArgs e)
        {
            var connection = (MetaPipeConnection)sender;
            if (connection != null)
            {
                connection.Disconnected -= Connection_Disconnected;

                lock (_connections)
                {
                    if (_connections.Remove(connection))
                        connection.Dispose();
                }
            }
        }

        void FireClientConnectedEvent(MetaPipeConnection connection)
        {
            try
            {
                ClientConnected?.Invoke(this, new PipeConnectionEventArgs(connection));
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "Unhandled exception in ClientConnected event handler");
            }
        }

        public void Stop()
        {
            try
            {
                _startStopLock.Wait();

                if (_worker != null)
                {
                    _cts.Cancel();

                    _worker.Wait();
                    _worker = null;

                    lock (_connections)
                    {
                        _connections.ForEach(c =>
                        {
                            c.Disconnected -= Connection_Disconnected;
                            c.Dispose();
                        });
                        _connections.Clear();
                    }

                    _cts.Dispose();
                    _cts = null;

                    _logger?.LogInformation("MetaPipeServer '{name}' stopped", PipeName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "MetaPipeServer '{name}' stop failed", PipeName);
                throw;
            }
            finally
            {
                _startStopLock.Release();
            }
        }


    }
}
