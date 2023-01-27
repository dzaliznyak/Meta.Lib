namespace Meta.Lib.Modules.PubSub.Messages
{
    public class PingReplay : PubSubMessageBase
    {
        public string Id { get; set; }

        public PingReplay()
        {
            ResponseTimeout = 5_000;
        }
    }
}
