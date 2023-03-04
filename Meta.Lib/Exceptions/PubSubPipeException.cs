using System;

namespace Meta.Lib.Exceptions
{
    internal class PubSubPipeException : Exception
    {
        public PubSubPipeException()
        {
        }

        public PubSubPipeException(string message) : base(message)
        {
        }

        public PubSubPipeException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}