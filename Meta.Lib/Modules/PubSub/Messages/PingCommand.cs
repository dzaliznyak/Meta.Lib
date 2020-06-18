namespace Meta.Lib.Modules.PubSub.Messages
{
    public class PingCommand : IPubSubMessage
    {
        public bool DeliverAtLeastOnce => true;

        public int Timeout => 1000;

        public byte[] Data { get; set; }
    }
}
