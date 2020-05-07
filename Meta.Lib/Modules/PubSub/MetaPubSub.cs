namespace Meta.Lib.Modules.PubSub
{
    public partial class MetaPubSub : IMetaPubSub
    {
        readonly DelayedMessages _delayedMessages;
        readonly MessageHub _messageHub;
        readonly DeliveryManager _deliveryManager;
        readonly RequestResponseProcessor _requestResponseProcessor;
        readonly MessageScheduler _messageScheduler;

        public MetaPubSub()
        {
            _delayedMessages = new DelayedMessages();
            _deliveryManager = new DeliveryManager(_delayedMessages.Put);
            _messageHub = new MessageHub(_deliveryManager.Put, _delayedMessages.OnNewSubscriber);
            _requestResponseProcessor = new RequestResponseProcessor(_messageHub);
            _messageScheduler = new MessageScheduler(_messageHub.Publish);
        }

    }
}
