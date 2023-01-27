namespace Meta.Lib.Modules.PubSub
{
    public interface IPubSubMessage
    {
        /// at least once delivery check - you can opt in to have an exception if no one subscribed to your message 
        bool DeliverAtLeastOnce { get; }
        /// time interval to wait for a subscriber (applicable if DeliverAtLeastOnce = true)
        int WaitForSubscriberTimeout { get; }

        /// Time interval during which the response message must be received otherwise the TimeoutException will be thrown
        int ResponseTimeout { get; }

        /// for internal use only
        string RemoteConnectionId { get; set; }
    }
}
