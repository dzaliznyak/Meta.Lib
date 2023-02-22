using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    public partial class MetaPubSub : IMetaPubSub
    {
        //readonly ILogger _logger;
        readonly DelayedMessages _delayedMessages;
        readonly MessageHub _messageHub;
        readonly DeliveryManager _deliveryManager;
        readonly RequestResponseProcessor _requestResponseProcessor;
        readonly MessageScheduler _messageScheduler;
        //readonly PipeConnectionsManager _pipeConections;
        //readonly RemotePubSubProxy _proxy;

        public MetaPubSub(ILogger logger = null)
        {
            //_logger = logger ?? MetaLogger.Default;

            _delayedMessages = new DelayedMessages();
            _deliveryManager = new DeliveryManager(logger, _delayedMessages.Put/*, PipeConections_Put*/);
            _messageHub = new MessageHub(logger, _deliveryManager.Put, _delayedMessages.OnNewSubscriber);
            //_pipeConections = new PipeConnectionsManager(_messageHub, logger, _delayedMessages.OnNewPipeSubscriber);
            _requestResponseProcessor = new RequestResponseProcessor(this);
            _messageScheduler = new MessageScheduler(_messageHub.Publish);
            //_proxy = new RemotePubSubProxy(_messageHub, logger);
        }

        //Task<bool> PipeConections_Put(IPubSubMessage arg0)
        //{
        //    return _pipeConections.Put(arg0);
        //}
    }
}
