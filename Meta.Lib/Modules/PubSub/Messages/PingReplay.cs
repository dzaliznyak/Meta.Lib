namespace Meta.Lib.Modules.PubSub.Messages
{
    public class PingReplay : IPubSubMessage
    {
        public bool DeliverAtLeastOnce => false;

        public int Timeout => 0;
    }
}
