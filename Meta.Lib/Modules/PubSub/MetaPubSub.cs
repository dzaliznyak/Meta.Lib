using Meta.Lib.Modules.Logger;

namespace Meta.Lib.Modules.PubSub
{
    public partial class MetaPubSub : IMetaPubSub
    {
        readonly IMetaLogger _logger;
        readonly DelayedMessages _delayedMessages;
        readonly MessageHub _messageHub;
        readonly DeliveryManager _deliveryManager;
        readonly RequestResponseProcessor _requestResponseProcessor;
        readonly MessageScheduler _messageScheduler;
        readonly PipeConnectionsManager _pipeConections;

        RemotePubSubProxy _proxy;

        public MetaPubSub(IMetaLogger logger = null)
        {
            _logger = logger ?? MetaLogger.Default;

            _delayedMessages = new DelayedMessages();
            _pipeConections = new PipeConnectionsManager(_logger, _delayedMessages.OnNewPipeSubscriber);
            _deliveryManager = new DeliveryManager(_logger, _delayedMessages.Put, _pipeConections.Put);
            _messageHub = new MessageHub(_logger, _deliveryManager.Put, _delayedMessages.OnNewSubscriber);
            _requestResponseProcessor = new RequestResponseProcessor(_messageHub);
            _messageScheduler = new MessageScheduler(_messageHub.Publish);
        }

    }
}
