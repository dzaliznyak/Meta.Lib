namespace Meta.Lib.Modules.PubSub
{
    class PubSubSettings
    {
        public static PubSubSettings Default { get; internal set; } = new PubSubSettings();
        public int SubscribeOnServerTimeout { get; set; } = 5_000;
        public int UnsubscribeOnServerTimeout { get; set; } = 5_000;
    }
}
