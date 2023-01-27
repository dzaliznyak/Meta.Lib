//using BenchmarkDotNet.Attributes;
//using Meta.Lib.Modules.PubSub;
//using Meta.Lib.Test;

/*
 * To use this benchmark uncomment ImmutableDictionaryNode and ConcurrentDictionaryNode in the Meta.Lib\Modules\PubSub\Node.cs
 * */

//namespace Meta.Lib.Benchmark
//{
//    [KeepBenchmarkFiles]
//    [RPlotExporter]
//    [MemoryDiagnoser]
//    [RankColumn]
//    public class NodeBenchmark : PubSubTestBase
//    {
//        private Node _node1 = default!;
//        private ImmutableDictionary_Node _nodeImmutableDictionary = default!;
//        private ConcurrentDictionary_Node _nodeConcurrentDictionary = default!;

//        [GlobalSetup]
//        public void GlobalSetup()
//        {
//            _node1 = new Node();

//            _node1.TryAdd<TestMessage0>(OnTestMessage0, null, out _);
//            _node1.TryAdd<TestMessage1>(OnTestMessage1, null, out _);
//            _node1.TryAdd<TestMessage2>(OnTestMessage2, null, out _);
//            _node1.TryAdd<TestMessage3>(OnTestMessage3, null, out _);
//            _node1.TryAdd<TestMessage4>(OnTestMessage4, null, out _);
//            _node1.TryAdd<TestMessage5>(OnTestMessage5, null, out _);
//            _node1.TryAdd<TestMessage6>(OnTestMessage6, null, out _);
//            _node1.TryAdd<TestMessage7>(OnTestMessage7, null, out _);
//            _node1.TryAdd<TestMessage8>(OnTestMessage8, null, out _);
//            _node1.TryAdd<TestMessage9>(OnTestMessage9, null, out _);

//            _nodeImmutableDictionary = new ImmutableDictionary_Node();

//            _nodeImmutableDictionary.TryAdd<TestMessage100>(OnTestMessage100, null, out _);
//            _nodeImmutableDictionary.TryAdd<TestMessage101>(OnTestMessage101, null, out _);
//            _nodeImmutableDictionary.TryAdd<TestMessage102>(OnTestMessage102, null, out _);
//            _nodeImmutableDictionary.TryAdd<TestMessage103>(OnTestMessage103, null, out _);
//            _nodeImmutableDictionary.TryAdd<TestMessage104>(OnTestMessage104, null, out _);
//            _nodeImmutableDictionary.TryAdd<TestMessage105>(OnTestMessage105, null, out _);
//            _nodeImmutableDictionary.TryAdd<TestMessage106>(OnTestMessage106, null, out _);
//            _nodeImmutableDictionary.TryAdd<TestMessage107>(OnTestMessage107, null, out _);
//            _nodeImmutableDictionary.TryAdd<TestMessage108>(OnTestMessage108, null, out _);
//            _nodeImmutableDictionary.TryAdd<TestMessage109>(OnTestMessage109, null, out _);

//            _nodeConcurrentDictionary = new ConcurrentDictionary_Node();

//            _nodeConcurrentDictionary.TryAdd<TestMessage200>(OnTestMessage200, null, out _);
//            _nodeConcurrentDictionary.TryAdd<TestMessage201>(OnTestMessage201, null, out _);
//            _nodeConcurrentDictionary.TryAdd<TestMessage202>(OnTestMessage202, null, out _);
//            _nodeConcurrentDictionary.TryAdd<TestMessage203>(OnTestMessage203, null, out _);
//            _nodeConcurrentDictionary.TryAdd<TestMessage204>(OnTestMessage204, null, out _);
//            _nodeConcurrentDictionary.TryAdd<TestMessage205>(OnTestMessage205, null, out _);
//            _nodeConcurrentDictionary.TryAdd<TestMessage206>(OnTestMessage206, null, out _);
//            _nodeConcurrentDictionary.TryAdd<TestMessage207>(OnTestMessage207, null, out _);
//            _nodeConcurrentDictionary.TryAdd<TestMessage208>(OnTestMessage208, null, out _);
//            _nodeConcurrentDictionary.TryAdd<TestMessage209>(OnTestMessage209, null, out _);


//            _hub = new MessageHub(null, OnPublished, OnNewSubscriber);
//            _hub.Subscribe<TestMessage300>(OnTestMessage300);
//            _hub.Subscribe<TestMessage301>(OnTestMessage301);
//            _hub.Subscribe<TestMessage302>(OnTestMessage302);
//            _hub.Subscribe<TestMessage303>(OnTestMessage303);
//            _hub.Subscribe<TestMessage304>(OnTestMessage304);
//            _hub.Subscribe<TestMessage305>(OnTestMessage305);
//            _hub.Subscribe<TestMessage306>(OnTestMessage306);
//            _hub.Subscribe<TestMessage307>(OnTestMessage307);
//            _hub.Subscribe<TestMessage308>(OnTestMessage308);
//            _hub.Subscribe<TestMessage309>(OnTestMessage309);
//        }

//        private void OnNewSubscriber(Type arg1, ISubscription arg2)
//        {
//        }

//        private Task OnPublished(IEnumerable<ISubscription> arg1, IPubSubMessage arg2)
//        {
//            return Task.CompletedTask;
//        }

//        [GlobalCleanup]
//        public void GlobalCleanup()
//        {
//        }

//        [Benchmark]
//        public void ImmutableArray_AddRemove()
//        {
//            _node1.TryAdd<TestMessage10>(OnTestMessage10, null, out _);
//            _node1.TryRemove<TestMessage10>(OnTestMessage10);
//        }

//        [Benchmark]
//        public void ImmutableDictionary_AddRemove()
//        {
//            _nodeImmutableDictionary.TryAdd<TestMessage110>(OnTestMessage110, null, out _);
//            _nodeImmutableDictionary.TryRemove<TestMessage110>(OnTestMessage110);
//        }

//        [Benchmark]
//        public void ConcurrentDictionary_AddRemove()
//        {
//            _nodeConcurrentDictionary.TryAdd<TestMessage210>(OnTestMessage210, null, out _);
//            _nodeConcurrentDictionary.TryRemove<TestMessage210>(OnTestMessage210);
//        }

//        [Benchmark]
//        public void ImmutableArray_Publish()
//        {
//            Publish(_node1.Subscribers, new TestMessage1());
//        }

//        [Benchmark]
//        public void ImmutableDictionary_Publish()
//        {
//            Publish2(_nodeImmutableDictionary.Subscribers, new TestMessage101());
//        }

//        [Benchmark]
//        public void ConcurrentDictionary_Publish()
//        {
//            Publish2(_nodeConcurrentDictionary.Subscribers, new TestMessage201());
//        }

//        internal static void Publish(IReadOnlyCollection<ISubscription> subscribers, IPubSubMessage message)
//        {
//            List<Exception> exceptions = null;

//            // deliver to local subscribers
//            foreach (var item in subscribers)
//            {
//                try
//                {
//                    if (item.ShouldDeliver(message))
//                    {
//                        //await item.Deliver(message);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    exceptions ??= new List<Exception>();
//                    exceptions.Add(ex);
//                }
//            }
//        }

//        internal static void Publish2(IEnumerable<ISubscription> subscribers, IPubSubMessage message)
//        {
//            List<Exception> exceptions = null;

//            // deliver to local subscribers
//            foreach (var item in subscribers)
//            {
//                try
//                {
//                    if (item.ShouldDeliver(message))
//                    {
//                        //await item.Deliver(message);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    exceptions ??= new List<Exception>();
//                    exceptions.Add(ex);
//                }
//            }
//        }

//    }
//}
