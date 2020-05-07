using System;
using System.Threading.Tasks;

//todo Timeout
//todo Cancellation
namespace Meta.Lib.Utils
{
    public class DelayedResult<T, TRes>
    {
        readonly object _lock = new object();
        readonly Action<T> _action;


        TaskCompletionSource<TRes> _tcs;

        public DelayedResult(Action<T> action)
        {
            _action = action;
        }

        public Task<TRes> Put(T item)
        {
            TaskCompletionSource<TRes> tcs = null;
            lock (_lock)
            {
                if (_tcs != null)
                    throw new InvalidOperationException("Cannot put a new item while previous is not finished");

                _tcs = new TaskCompletionSource<TRes>();
                tcs = _tcs;
            }

            _action.Invoke(item);
            return tcs.Task;
        }

        public void OnResult(TRes result)
        {
            TaskCompletionSource<TRes> tcs = null;
            lock (_lock)
            {
                tcs = _tcs;
                _tcs = null;
            }

            if (tcs != null)
                tcs.SetResult(result);
            else
                throw new InvalidOperationException("Put() not called or Result already gotten");
        }

        public void OnException(Exception exception)
        {
            TaskCompletionSource<TRes> tcs = null;
            lock (_lock)
            {
                tcs = _tcs;
                _tcs = null;
            }

            if (tcs != null)
                tcs.SetException(exception);
            else
                throw new InvalidOperationException("Put() not called or Result already gotten");
        }
    }
}
