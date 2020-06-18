using System;

namespace Meta.Lib.Examples
{
    class Program
    {
        static void Main(string[] args)
        {
            var interproc = new InterprocPubSubTest();
            interproc.RunAllExamples();

            //var examples = new PubSubExample();
            //examples.RunAllExamples();

            Console.ReadLine();
        }
    }
}
