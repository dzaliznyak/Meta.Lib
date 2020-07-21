using System;

namespace Meta.Lib.Modules.PubSub.Messages
{
    public class ConnectedToServerEvent : PubSubMessageBase
    {
        public DateTime Timestamp { get; internal set; }
    }
}
