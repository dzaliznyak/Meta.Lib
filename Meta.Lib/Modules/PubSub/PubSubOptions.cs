namespace Meta.Lib.Modules.PubSub
{
    public class PubSubOptions
    {
        /// at least once delivery check - you can opt in to have an exception if no one subscribed to your message 
        public bool DeliverAtLeastOnce { get; set; } = false;

        /// time interval to wait for a subscriber (applicable if DeliverAtLeastOnce = true)
        public int WaitForSubscriberTimeout { get; set; } = 0;
    }
}
