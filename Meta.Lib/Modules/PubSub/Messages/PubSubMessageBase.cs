namespace Meta.Lib.Modules.PubSub
{
    public class PubSubMessageBase : IPubSubMessage
    {
        public bool DeliverAtLeastOnce { get; set; }

        public int WaitForSubscriberTimeout { get; set; }

        public int ResponseTimeout { get; set; } = 5_000;

        public string RemoteConnectionId { get; set; }
    }
}
