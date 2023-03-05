using System;

namespace Meta.Lib.Examples
{
    class Program
    {
        static void Main(string[] args)
        {
            var remote = new PubSubPipeExample();
            remote.RunAllExamples();

            //var examples = new PubSubExample();
            //examples.RunAllExamples();

            Console.ReadLine();
        }
    }
}
