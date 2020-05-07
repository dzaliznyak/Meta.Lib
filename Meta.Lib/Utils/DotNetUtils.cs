using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Utils
{
    public static class DotNetUtils
    {
        public static async Task TimeoutAfter(this Task task, int millisecondsTimeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(millisecondsTimeout)))
                await task;
            else
                throw new TimeoutException();
        }

        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, int millisecondsTimeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(millisecondsTimeout)))
                return await task;
            else
                throw new TimeoutException();
        }

        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (task == await Task.WhenAny(task, Task.Delay(millisecondsTimeout, cancellationToken)))
            {
                return await task;
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException();
            }
        }
    }
}
