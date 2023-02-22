using System.Threading.Tasks;

namespace Meta.Lib.Modules.Pipe
{
    internal interface IResponseAwaiter
    {
        void SetException(RemoteException exception);
        void SetResult(object obj);
    }

    internal class ResponseAwaiter<TResponse> : IResponseAwaiter
    {
        public TaskCompletionSource<TResponse> Tcs { get; }
            = new TaskCompletionSource<TResponse>();

        public void SetResult(object obj)
        {
            Tcs.SetResult((TResponse)obj);
        }

        public void SetException(RemoteException exception)
        {
            Tcs.SetException(exception);
        }
    }
}
