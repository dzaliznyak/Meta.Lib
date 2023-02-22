using System;

namespace Meta.Lib.Modules.Pipe
{
    public class RemoteException : Exception
    {
        public ErrorDescription Error { get; }

        public RemoteException(ErrorDescription error)
        {
            Error = error;
        }
    }
}
