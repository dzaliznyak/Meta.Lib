using System;

namespace Meta.Lib.Modules.PubSub.Messages
{
    public class DisconnectedFromServerEvent : PubSubMessageBase
    {
        public DateTime Timestamp { get; internal set; }
        public bool LostConnection { get; internal set; }
    }
}
