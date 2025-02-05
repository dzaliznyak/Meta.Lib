using System;

namespace Meta.Lib.Messages
{
    public class DisconnectedFromServerEvent
    {
        public DateTime Timestamp { get; internal set; }
        public bool LostConnection { get; internal set; }
    }
}
