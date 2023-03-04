using Meta.Lib.Modules.Pipe;
using System;

namespace Meta.Lib.Exceptions
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
