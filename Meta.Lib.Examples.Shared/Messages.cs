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
