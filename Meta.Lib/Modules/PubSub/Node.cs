using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

/*
 
ImmutableArray has best performance for enumerating the collection (Publish), 
which is most important than Add/Remove (Subscribe/Unsubscribe)
operations. Tested on 10 items in the array.

|                         Method |        Mean |     Error |    StdDev | Rank |   Gen0 | Allocated |
|------------------------------- |------------:|----------:|----------:|-----:|-------:|----------:|
|       ImmutableArray_AddRemove |   264.43 ns |  2.744 ns |  2.567 ns |    3 | 0.0896 |     376 B |
|  ImmutableDictionary_AddRemove |   570.61 ns |  3.892 ns |  3.640 ns |    5 | 0.1888 |     792 B |
| ConcurrentDictionary_AddRemove |   148.48 ns |  2.828 ns |  3.256 ns |    2 | 0.0744 |     312 B |
|         ImmutableArray_Publish |    90.03 ns |  1.261 ns |  1.053 ns |    1 | 0.0267 |     112 B |
|    ImmutableDictionary_Publish | 1,185.89 ns | 23.707 ns | 35.483 ns |    6 | 0.0515 |     216 B |
|   ConcurrentDictionary_Publish |   491.58 ns |  3.772 ns |  3.528 ns |    4 | 0.0610 |     256 B |

*/

namespace Meta.Lib.Modules.PubSub
{
    public class Node
    {
        ImmutableArray<ISubscription> _subscribers = ImmutableArray<ISubscription>.Empty;

        public IReadOnlyCollection<ISubscription> Subscribers => _subscribers;

        static ISubscription Find(ImmutableArray<ISubscription> array, object handler)
        {
            foreach (var item in array)
            {
                if (item.IsSameHandler(handler))
                    return item;
            }

            return null;
        }

        public bool TryAdd<TMessage>(Func<TMessage, Task> handler, Predicate<TMessage> filter, out ISubscription subscription)
            where TMessage : class, IPubSubMessage
        {
            subscription = null;
            var snapshot = _subscribers;

            bool success;
            do
            {
                if (Find(snapshot, handler) != null)
                    return false;

                if (subscription == null)
                    subscription = new Subscription<TMessage>(handler, filter);

                var newArray = snapshot.Add(subscription);

                var exchangeRes = ImmutableInterlocked.InterlockedCompareExchange(ref _subscribers, newArray, snapshot);
                success = snapshot == exchangeRes;
                if (!success)
                    snapshot = exchangeRes;
            }
            while (!success);

            return true;
        }

        public bool TryRemove<TMessage>(Func<TMessage, Task> handler)
            where TMessage : class, IPubSubMessage
        {
            var snapshot = _subscribers;

            bool success;
            do
            {
                var subscription = Find(snapshot, handler);

                if (subscription == null)
                    return false;

                var newArray = snapshot.Remove(subscription);

                var exchangeRes = ImmutableInterlocked.InterlockedCompareExchange(ref _subscribers, newArray, snapshot);
                success = snapshot == exchangeRes;
                if (!success)
                    snapshot = exchangeRes;
            }
            while (!success);

            return true;
        }
    }



}

/*

    public class ImmutableDictionary_Node
    {
        ImmutableDictionary<object, ISubscription> _subscribers = ImmutableDictionary<object, ISubscription>.Empty;
        public IEnumerable<ISubscription> Subscribers => _subscribers.Values;

        public bool TryAdd<TMessage>(Func<TMessage, Task> handler, Predicate<TMessage> filter, out ISubscription subscription)
            where TMessage : class, IPubSubMessage
        {
            bool added = false;
            subscription = ImmutableInterlocked.AddOrUpdate(ref _subscribers, handler,
                h =>
                {
                    added = true;
                    return new Subscription<TMessage>(handler, filter);
                },
                (u, s) => { return s; });

            return added;
        }

        public bool TryRemove<TMessage>(Func<TMessage, Task> action)
            where TMessage : class, IPubSubMessage
        {
            return ImmutableInterlocked.TryRemove(ref _subscribers, action, out _);
        }
    }

    public class ConcurrentDictionary_Node
    {
        readonly ConcurrentDictionary<object, ISubscription> _subscribers = new ConcurrentDictionary<object, ISubscription>();
        public ICollection<ISubscription> Subscribers => _subscribers.Values;

        public bool TryAdd<TMessage>(Func<TMessage, Task> action, Predicate<TMessage> filter, out ISubscription subscription)
            where TMessage : class, IPubSubMessage
        {
            bool added = false;
            subscription = _subscribers.GetOrAdd(action, a =>
            {
                added = true;
                return new Subscription<TMessage>(action, filter);
            });
            return added;
        }

        public bool TryRemove<TMessage>(Func<TMessage, Task> action)
            where TMessage : class, IPubSubMessage
        {
            return _subscribers.TryRemove(action, out _);
        }
    }


 
 * */
