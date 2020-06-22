using Meta.Lib.Modules.Logger;
using Meta.Lib.Modules.PubSub;
using System;

namespace Meta.Lib.Examples.Shared
{
    public class MyMessage : PubSubMessageBase
    {
        public int SomeId { get; set; }
        public int DeliveredCount { get; set; }
        public MetaLogErrorSeverity LogSeverity { get; set; }
        public string Message { get; set; }
        public byte[] Data { get; set; }

        public MyMessage()
        {
        }

        public MyMessage(string message)
        {
            Message = message;
        }
    }

    public class MyMessage2 : PubSubMessageBase
    {
        public int SomeId { get; set; }
        public int DeliveredCount { get; set; }
        public MetaLogErrorSeverity LogSeverity { get; set; }
    }

    public class MyEvent : MyMessage
    {
    }
}
