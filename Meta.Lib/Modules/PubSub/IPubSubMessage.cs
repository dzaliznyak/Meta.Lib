namespace Meta.Lib.Modules.PubSub
{
    public interface IPubSubMessage
    {
        bool DeliverAtLeastOnce { get; }
        int Timeout { get; }
    }
}
