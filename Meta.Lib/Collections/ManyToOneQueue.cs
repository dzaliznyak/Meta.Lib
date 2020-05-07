using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Collections
{
    public class ManyToOneQueue<T>
    {
        readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        readonly Func<T, Task> _output;
        int _working;

        public ManyToOneQueue(Func<T, Task> output)
        {
            _output = output;
        }

        public void Enqueue(T item)
        {
            _queue.Enqueue(item);
            TryStart();
        }

        async void TryStart()
        {
            if (Interlocked.CompareExchange(ref _working, 1, 0) == 0)
            {
                while (_queue.TryDequeue(out T item))
                {
                    try
                    {
                        await _output(item);
                    }
                    catch (Exception)
                    {
                        //todo - log 
                    }
                }

                //todo - если Enqueue будет вызван в этот момент,
                // то добавленный элемент не обработается
                Interlocked.Exchange(ref _working, 0);
            }
        }


    }
}
