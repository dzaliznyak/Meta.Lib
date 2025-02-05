using System;

namespace Meta.Lib.Examples
{
    class Program
    {
        static void Main(string[] args)
        {
            //var examples = new PubSubExample();
            //examples.RunAllExamples();

            var remote = new PubSubPipeExample();
            remote.RunAllExamples();

            Console.ReadLine();
        }
    }
}
