using System;

namespace Meta.Lib.Examples.Shared
{
    public class MyMessage
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

    public class MyMessageResponse
    {
        public int SomeId { get; set; }
        public int DeliveredCount { get; set; }
        public string Message { get; set; }
        public byte[] Data { get; set; }
        public Version Version { get; set; }

        public MyMessageResponse()
        {
        }

        public MyMessageResponse(string message)
        {
            Message = message;
        }
    }

    public class MyMessage2
    {
        public int SomeId { get; set; }
        public int DeliveredCount { get; set; }
    }

    public class MyEvent : MyMessage
    {
    }
}
