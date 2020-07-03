namespace Meta.Lib.Modules.PubSub
{
    public class PubSubMessageBase : IPubSubMessage
    {
        public bool DeliverAtLeastOnce { get; set; }

        public int Timeout { get; set; }

        public string RemoteConnectionId { get; set; }
    }
}
