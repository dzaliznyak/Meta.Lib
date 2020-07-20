using System;
using System.Runtime.Serialization;

namespace Meta.Lib.Modules.PubSub
{
    [Serializable]
    public class NotConnectedToServerException : Exception
    {
        public NotConnectedToServerException(string message)
            : base(message)
        {
        }

        public NotConnectedToServerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public NotConnectedToServerException()
        {
        }

        public NotConnectedToServerException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

    }
}
