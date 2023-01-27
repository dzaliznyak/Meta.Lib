using BenchmarkDotNet.Running;

namespace Meta.Lib.Benchmark
{
    internal class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<PubSubBenchmark>();
            //BenchmarkRunner.Run<NodeBenchmark>();

            // debug for NodeBenchmark ------------------
            //var nb = new NodeBenchmark();
            //nb.GlobalSetup();
            //nb.AddRemoveAsArray();
            //nb.Publish1();
            //nb.Publish2();
            //nb.Publish3();
            //nb.GlobalCleanup();

            // debug for PubSubBenchmark ---------------
            //var bm = new PubSubBenchmark();
            //bm.GlobalSetup();
            //for (int i = 0; i < 10; i++)
            //{
            //    bm.Subscribe1();
            //}
            //bm.GlobalCleanup();

            Console.WriteLine("Benchmark end!");
            Console.ReadLine();
        }

    }
}