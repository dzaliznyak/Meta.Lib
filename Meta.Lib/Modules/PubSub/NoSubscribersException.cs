using System;

namespace Meta.Lib.Modules.PubSub
{
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
    }
}
