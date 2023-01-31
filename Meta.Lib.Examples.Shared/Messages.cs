using Meta.Lib.Modules.PubSub;
using System;

namespace Meta.Lib.Examples.Shared
{
    public class MyMessage : PubSubMessageBase
    {
        public int SomeId { get; set; }
        public int DeliveredCount { get; set; }
        public string Message { get; set; }
        public byte[] Data { get; set; }
        public Version Version { get; set; }

        public MyMessage()
        {
        }

        public MyMessage(string message)
        {
            Message = message;
        }
    }

    public class MyMessageReplay : PubSubMessageBase
    {
        public int SomeId { get; set; }
        public int DeliveredCount { get; set; }
        public string Message { get; set; }
        public byte[] Data { get; set; }
        public Version Version { get; set; }

        public MyMessageReplay()
        {
        }

        public MyMessageReplay(string message)
        {
            Message = message;
        }
    }

    public class MyMessage2 : PubSubMessageBase
    {
        public int SomeId { get; set; }
        public int DeliveredCount { get; set; }
    }

    public class MyEvent : MyMessage
    {
    }
}
