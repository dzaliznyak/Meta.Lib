namespace Meta.Lib.Modules.PubSubPipe
{
    public class PubSubPipeSettings
    {
        public static PubSubPipeSettings Default { get; internal set; } = new PubSubPipeSettings();

        public int SubscribeOnServerTimeout { get; set; } = 5_000;
        public int UnsubscribeOnServerTimeout { get; set; } = 5_000;
    }
}
