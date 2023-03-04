using System;

namespace Meta.Lib.Exceptions
{
    public class NotConnectedToServerException : Exception
    {
        public NotConnectedToServerException()
            : base("Not connected to the server")
        {
        }
    }
}
