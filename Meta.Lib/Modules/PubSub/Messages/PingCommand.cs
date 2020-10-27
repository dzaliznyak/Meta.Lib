namespace Meta.Lib.Modules.PubSub.Messages
{
    public class PingCommand : PubSubMessageBase
    {
        public string Id { get; set; }

        public byte[] Data { get; set; }

        public PingCommand()
        {
            DeliverAtLeastOnce = true;
            WaitForSubscriberTimeout = 1_000;
            ResponseTimeout = 5_000;
        }

    }
}
