using Meta.Lib.Modules.Pipe;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSubPipe
{
    /// <summary>
    /// Represents client connection via pipe on the server side
    /// </summary>
    internal class RemoteClientConnection : IDisposable
    {
        readonly IMetaPubSub _pubSub;
        readonly ILogger _logger;
        readonly MetaPipeConnection _pipe;

        readonly ReferenceCountedCollection<Type> _subscribedTypes =
            new ReferenceCountedCollection<Type>();

        public bool IsConnected => _pipe.IsConnected;

        public RemoteClientConnection(MetaPipeConnection pipe, IMetaPubSub pubSub, ILogger logger)
        {
            _pipe = pipe;
            _logger = logger;
            _pubSub = pubSub;
            _pipe.MessageReceived += Pipe_MessageReceived;
            _pipe.Disconnected += Pipe_Disconnected;
        }

        public void Dispose()
        {
            _pipe.MessageReceived -= Pipe_MessageReceived;
            _pipe.Disconnected -= Pipe_Disconnected;
        }

        protected void Pipe_MessageReceived(object sender, PipeMessageEventArgs e)
        {
            Task.Run(async () =>
            {
                // Only requests are processed here.
                // Responses are processed by the pipe.SendAndWaitResponse()
                if (e.Obj is RemoteSubscribeRequest subscribeRequest)
                {
                    await OnSubscribeRequest(subscribeRequest, e.CorrelationId);
                }
                else if (e.Obj is RemoteUnsubscribeRequest unsubscribeRequest)
                {
                    await OnUnsubscribeRequest(unsubscribeRequest, e.CorrelationId);
                }
                else if (e.Obj is RemotePublishRequest publishRequest)
                {
                    await OnPublishRequest(publishRequest, e.CorrelationId);
                }
                else if (e.Obj is RemoteProcessRequest processRequest)
                {
                    await OnProcessRequest(processRequest, e.CorrelationId);
                }
                else if (!(e.Obj is RemoteSystemMessage))
                {
                    Debug.Assert(false);
                }
            });
        }

        void Pipe_Disconnected(object sender, EventArgs e)
        {
            foreach (var item in _subscribedTypes.GetItems())
            {
                _pubSub.Unsubscribe(item, OnMessageFromLocalPubSub);
            }
            _subscribedTypes.Clear();
        }

        // receives all subscribed messages regardless of their type
        protected async Task OnMessageFromLocalPubSub(object message)
        {
            var request = new RemotePublishRequest(message, options: null);

            var response = await _pipe.SendAndWaitResponse<RemotePublishResponse>(
                typeof(RemotePublishRequest), request, Guid.NewGuid());

            response.ThrowIfError();
        }

        async Task OnSubscribeRequest(RemoteSubscribeRequest subscribeRequest, Guid correlationId)
        {
            try
            {
                Type type = Type.GetType(subscribeRequest.TypeName);

                if (_subscribedTypes.Add(type))
                {
                    _pubSub.Subscribe(type, OnMessageFromLocalPubSub);
                    _logger?.LogInformation("Client subscribed to {MessageType}", type);
                }

                await _pipe.Send(new RemoteSubscribeResponse(), correlationId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error sending subscribe response");
            }
        }

        async Task OnUnsubscribeRequest(RemoteUnsubscribeRequest unsubscribeRequest, Guid correlationId)
        {
            try
            {
                Type type = Type.GetType(unsubscribeRequest.TypeName);
                if (_subscribedTypes.Remove(type))
                {
                    _pubSub.Unsubscribe(type, OnMessageFromLocalPubSub);
                    _logger?.LogInformation("Client unsubscribed from {MessageType}", type);
                }

                await _pipe.Send(new RemoteUnsubscribeResponse(), correlationId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error sending unsubscribe response");
            }
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

        async Task OnProcessRequest(RemoteProcessRequest processRequest, Guid correlationId)
        {
            try
            {
                object result = null;
                try
                {
                    object message = processRequest.GetMessage();
                    Type responseType = Type.GetType(processRequest.ResponseTypeName);

                    result = await _pubSub.Process(
                        responseType,
                        message,
                        processRequest.Timeout,
                        processRequest.Options);
                }
                catch (Exception ex)
                {
                    await _pipe.Send(new RemoteProcessResponse(ex), correlationId);
                    return;
                }

                await _pipe.Send(new RemoteProcessResponse(result), correlationId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error sending publish response");
            }
        }

    }
}
