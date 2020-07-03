using System;
using System.Runtime.Serialization;

namespace Meta.Lib.Modules.PubSub
{
    [Serializable]
    public class NoSubscribersException : Exception
    {
        public NoSubscribersException(string message)
            : base(message)
        {
        }

        public NoSubscribersException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public NoSubscribersException()
        {
        }

        public NoSubscribersException(SerializationInfo info, StreamingContext context)
            :base(info, context)
        {
        }

    }
}
