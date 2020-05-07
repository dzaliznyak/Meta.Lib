using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

//todo Timeout
//todo Cancellation
//todo progress
//todo upper limit
namespace Meta.Lib.Collections
{
    public class AwaitableManyToOneQueue<T, TRes>
    {
        class QueueItem<TT, TTRes>
        {
            public TaskCompletionSource<TTRes> Tcs { get; }
                = new TaskCompletionSource<TTRes>();
            public TT Item { get; }

            public QueueItem(TT item)
            {
                Item = item;
            }
        }

        readonly ConcurrentQueue<QueueItem<T, TRes>> _queue =
            new ConcurrentQueue<QueueItem<T, TRes>>();
        readonly Func<T, Task<TRes>> _output;

        int _working;

        public AwaitableManyToOneQueue(Func<T, Task<TRes>> output)
        {
            _output = output;
        }

        public Task<TRes> Enqueue(T item)
        {
            var meta = new QueueItem<T, TRes>(item);
            _queue.Enqueue(meta);
            TryStart();
            return meta.Tcs.Task;
        }

        async void TryStart()
        {
            if (Interlocked.CompareExchange(ref _working, 1, 0) == 0)
            {
                while (_queue.TryDequeue(out QueueItem<T, TRes> item))
                    await ProcessItem(item);

                //todo - если Enqueue будет вызван в этот момент,
                // то добавленный элемент не обработается
                Interlocked.Exchange(ref _working, 0);
            }
        }

        async Task ProcessItem(QueueItem<T, TRes> item)
        {
            try
            {
                var res = await _output(item.Item);
                item.Tcs.SetResult(res);
            }
            catch (Exception ex)
            {
                item.Tcs.SetException(ex);
            }
        }
    }
}
