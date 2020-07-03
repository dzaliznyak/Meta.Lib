namespace Meta.Lib.Modules.PubSub.Messages
{
    public class PingReplay : IPubSubMessage
    {
        public bool DeliverAtLeastOnce => false;

        public int Timeout => 0;

        public string RemoteConnectionId { get; set; }

        public string Id { get; set; }
    }
}
