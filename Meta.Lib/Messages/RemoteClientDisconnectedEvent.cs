using System;

namespace Meta.Lib.Messages
{
    public class RemoteClientDisconnectedEvent
    {
        public DateTime Timestamp { get; internal set; }
        public int TotalClientsCount { get; internal set; }
    }
}
