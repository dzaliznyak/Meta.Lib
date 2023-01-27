namespace Meta.Lib.TestFilesGenerator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            FileGenerator.GenerateMessageTypes(1000);
            FileGenerator.GenerateTestBase(1000);

            Console.WriteLine("Files created!");
        }
    }
}