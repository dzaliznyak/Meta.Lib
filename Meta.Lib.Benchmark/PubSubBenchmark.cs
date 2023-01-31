using BenchmarkDotNet.Attributes;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Test;

namespace Meta.Lib.Benchmark
{
    public class MyMessage : PubSubMessageBase
    {
        public int SomeId { get; set; }
        public int DeliveredCount { get; set; }
        public string Message { get; set; }
        public byte[] Data { get; set; }
        public Version Version { get; set; }

        public MyMessage()
        {
        }

        public MyMessage(string message)
        {
            Message = message;
        }
    }

    [KeepBenchmarkFiles]
    [MemoryDiagnoser]
    [RankColumn]
    public class PubSubBenchmark : PubSubTestBase
    {
        // message handler
        Task OnMyMessage(MyMessage message)
        {
            //Console.WriteLine($"OnMyMessage called at {DateTime.Now:HH:mm:ss.fff}");
            return Task.CompletedTask;
        }

        [GlobalSetup]
        public void GlobalSetup()
        {
            //_hub = new MetaPubSub();
            SubscribeAll();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            UnsubscribeAll();
        }

        [Benchmark]
        public void Subscribe0()
        {
            // this message already has a lot of subscribers
            _hub.Subscribe<TestMessage0>(OnTestMessage0);
        }

        [Benchmark]
        public void Subscribe1()
        {
            _hub.Subscribe<TestMessage1>(OnTestMessage1);
        }


        [Benchmark]
        public void SubscribeUnsubscribe()
        {
            _hub.Subscribe<TestMessage0>(OnTestMessage0);
            _hub.Unsubscribe<TestMessage0>(OnTestMessage0);
        }

        [Benchmark]
        public async Task PublishFirstMessage()
        {
            var message = new TestMessage0() { StringProperty = "test message" };
            await _hub.Publish(message);
        }

        [Benchmark]
        public async Task PublishMiddleMessage()
        {
            await _hub.Publish(new TestMessage500() { StringProperty = "test message" });
        }

        [Benchmark]
        public async Task PublishLastMessage()
        {
            await _hub.Publish(new TestMessage999() { StringProperty = "test message" });
        }

    }
}
