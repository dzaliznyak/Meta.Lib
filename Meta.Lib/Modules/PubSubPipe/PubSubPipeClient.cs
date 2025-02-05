using Meta.Lib.Exceptions;
using Meta.Lib.Modules.Pipe;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSubPipe
{
    /// <inheritdoc />
    public class PubSubPipeClient : IPubSubPipeClient, IDisposable
    {
        readonly IMetaPubSub _pubSub;
        readonly ILogger _logger;
        readonly MetaPipeConnection _pipe;
        readonly SemaphoreSlim _subscribeLock = new SemaphoreSlim(1, 1);
        readonly ManualResetEvent _reconnectEvent = new ManualResetEvent(false);

        public event EventHandler Connected;
        public event EventHandler Disconnected;

        readonly ReferenceCountedCollection<Type> _subscribedTypes = new ReferenceCountedCollection<Type>();

        public bool IsConnected => _pipe.IsConnected;
        public IMetaPubSub MetaPubSub => _pubSub;

        public PubSubPipeClient(
            string pipeName,
            IMetaPubSub pubSub,
            ILogger logger = null,
            int connectTimeout = 5000,
            bool autoReconnect = true,
            string serverName = ".")
        {
            _pubSub = pubSub;
            _logger = logger;
            _pipe = new MetaPipeConnection(pipeName, logger, connectTimeout, autoReconnect, serverName);
            _pipe.MessageReceived += Pipe_MessageReceived;
            _pipe.Connected += Pipe_Connected;
            _pipe.Disconnected += Pipe_Disconnected;
        }

        public void Dispose()
        {
            _pipe.MessageReceived -= Pipe_MessageReceived;
            _pipe.Connected -= Pipe_Connected;
            _pipe.Dispose();
            _subscribeLock.Dispose();
            _reconnectEvent.Dispose();
        }

        void Pipe_Connected(object sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                await ResubscribeAllMessages();
                _reconnectEvent.Set();
                OnConnected(EventArgs.Empty);
            });
        }

        void Pipe_Disconnected(object sender, EventArgs e)
        {
            _reconnectEvent.Reset();
            OnDisconnected(EventArgs.Empty);
        }

        protected virtual void OnConnected(EventArgs e)
        {
            try
            {
                Connected?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in Connected event handler");
            }
        }

        protected virtual void OnDisconnected(EventArgs e)
        {
            try
            {
                Disconnected?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in Disconnected event handler");
            }
        }

        void Pipe_MessageReceived(object sender, PipeMessageEventArgs e)
        {
            Task.Run(async () =>
            {
                // Only requests are processed here.
                // Responses are processed by the pipe.SendAndWaitResponse()
                if (e.Obj is RemotePublishRequest publishRequest)
                {
                    await OnPublishRequest(publishRequest, e.CorrelationId);
                }
                else if (!(e.Obj is RemoteSystemMessage))
                {
                    Debug.Assert(false);
                }
            });
        }

        async Task OnPublishRequest(RemotePublishRequest publishRequest, Guid correlationId)
        {
            try
            {
                try
                {
                    await _pubSub.Publish(publishRequest.GetMessage(), publishRequest.Options);
                }
                catch (Exception ex)
                {
                    await _pipe.Send(new RemotePublishResponse(ex), correlationId);
                    return;
                }

                await _pipe.Send(new RemotePublishResponse(), correlationId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error sending publish response");
            }
        }

        public Task ConnectToServer()
        {
            return _pipe.Connect();
        }

        public async Task<bool> TryConnectToServer()
        {
            try
            {
                await _pipe.Connect();
                return true;
            }
            catch (Exception)
            {
            }

            _pipe.Reconnect();
            return false;
        }

        public void DisconnectFromServer()
        {
            _pipe.Disconnect();
        }

        public async Task SubscribeOnServer<TMessage>()
        {
            if (!IsConnected) throw new NotConnectedToServerException();

            try
            {
                await _subscribeLock.WaitAsync();

                var request = new RemoteSubscribeRequest(typeof(TMessage));

                var response = await _pipe.SendAndWaitResponse<RemoteSubscribeRequest, RemoteSubscribeResponse>(
                    request,
                    Guid.NewGuid(),
                    PubSubPipeSettings.Default.SubscribeOnServerTimeout);

                response.ThrowIfError();

                _subscribedTypes.Add(typeof(TMessage));
            }
            finally
            {
                _subscribeLock.Release();
            }
        }

        public async Task UnsubscribeOnServer<TMessage>()
        {
            if (!IsConnected) throw new NotConnectedToServerException();

            try
            {
                await _subscribeLock.WaitAsync();

                var request = new RemoteUnsubscribeRequest(typeof(TMessage));

                var response = await _pipe.SendAndWaitResponse<RemoteUnsubscribeRequest, RemoteUnsubscribeResponse>(
                    request,
                    Guid.NewGuid(),
                    PubSubPipeSettings.Default.UnsubscribeOnServerTimeout);

                response.ThrowIfError();

                _subscribedTypes.Remove(typeof(TMessage));
            }
            finally
            {
                _subscribeLock.Release();
            }
        }

        public async Task<bool> TrySubscribeOnServer<TMessage>()
        {
            try
            {
                await _subscribeLock.WaitAsync();

                // adding regardless of the subscribe result
                _subscribedTypes.Add(typeof(TMessage));

                if (!IsConnected)
                    return false;

                var request = new RemoteSubscribeRequest(typeof(TMessage));

                var response = await _pipe.SendAndWaitResponse<RemoteSubscribeRequest, RemoteSubscribeResponse>(
                    request,
                    Guid.NewGuid(),
                    PubSubPipeSettings.Default.SubscribeOnServerTimeout);

                response.ThrowIfError();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _subscribeLock.Release();
            }
        }

        async Task ResubscribeAllMessages()
        {
            try
            {
                await _subscribeLock.WaitAsync();

                foreach (var type in _subscribedTypes.GetItems())
                {
                    var request = new RemoteSubscribeRequest(type);

                    var response = await _pipe.SendAndWaitResponse<RemoteSubscribeRequest, RemoteSubscribeResponse>(
                        request,
                        Guid.NewGuid(),
                        PubSubPipeSettings.Default.SubscribeOnServerTimeout);

                    response.ThrowIfError();

                    if (!IsConnected)
                        return;
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                _subscribeLock.Release();
            }
        }

        public async Task PublishOnServer<TMessage>(TMessage message, PubSubOptions options = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (!IsConnected) throw new NotConnectedToServerException();

            var request = new RemotePublishRequest(message, options);

            var response = await _pipe.SendAndWaitResponse<RemotePublishResponse>(
                typeof(RemotePublishRequest), request, Guid.NewGuid());

            response.ThrowIfError();
        }

        public async Task<TResponse> ProcessOnServer<TMessage, TResponse>(
            TMessage message,
            int responseTimeoutMs = 5000,
            PubSubOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsConnected) throw new NotConnectedToServerException();

            var request = new RemoteProcessRequest(message, typeof(TResponse), responseTimeoutMs, options);

            var response = await _pipe.SendAndWaitResponse<RemoteProcessResponse>(
                typeof(RemoteProcessRequest),
                request,
                Guid.NewGuid(),
                timeout: responseTimeoutMs + 5000, // give some extra time to Send/Receive message
                cancellationToken);

            response.ThrowIfError();

            return response.GetResult<TResponse>();
        }

        public void WaitForReconnect(int millisecondsTimeout)
        {
            _reconnectEvent.WaitOne(millisecondsTimeout);
        }
    }
}
