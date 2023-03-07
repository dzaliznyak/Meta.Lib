namespace Meta.Lib.TestFilesGenerator
{
    internal class FileGenerator
    {
        public static void GenerateMessageTypes(int count)
        {
            using StreamWriter file = new("..\\..\\..\\..\\Meta.Lib.Benchmark\\Messages.cs", append: false);

            string txt =
                    $$"""
                    using Meta.Lib.Modules.PubSub;

                    namespace Meta.Lib.Test
                    {
                    """;
            file.WriteLine(txt);

            for (int i = 0; i < count; i++)
            {
                txt =
                    $$"""
                        public class TestMessage{{i}}
                        {
                            public int IntProperty { get; set; }
                            public string StringProperty { get; set; }
                        }
                    """;

                file.WriteLine(txt);
            }

            file.WriteLine("}");
        }

        public static void GenerateTestBase(int count)
        {
            using StreamWriter file = new("..\\..\\..\\..\\Meta.Lib.Benchmark\\PubSubTestBase.cs", append: false);

            string txt =
                    $$"""
                    using Meta.Lib.Modules.PubSub;
                    using Meta.Lib.Test;

                    namespace Meta.Lib.Benchmark
                    {
                        public class PubSubTestBase
                        {
                            protected MetaPubSub _hub = default!;


                            #region Handlers
                    """;
            file.WriteLine(txt);

            // handlers
            for (int i = 0; i < count; i++)
            {
                txt =
                    $$"""
                            protected static Task OnTestMessage{{i}}(TestMessage{{i}} message)
                            {
                                message.IntProperty++;
                                return Task.CompletedTask;
                            }
                            protected static Task OnTestMessage0_{{i}}(TestMessage0 message)
                            {
                                message.IntProperty++;
                                return Task.CompletedTask;
                            }
                    """;

                file.WriteLine(txt);
            }

            txt =
                $$"""
                        #endregion Handlers

                        public void SubscribeAll()
                        {
                """;
            file.WriteLine(txt);

            // subscribe
            for (int i = 0; i < count; i++)
            {
                txt =
                    $$"""
                                _hub.Subscribe<TestMessage{{i}}>(OnTestMessage{{i}});
                                _hub.Subscribe<TestMessage0>(OnTestMessage0_{{i}});
                    """;

                file.WriteLine(txt);
            }

            txt =
                $$"""
                        }

                        public void UnsubscribeAll()
                        {
                """;
            file.WriteLine(txt);

            // unsubscribe
            for (int i = 0; i < count; i++)
            {
                txt =
                    $$"""
                                _hub.Unsubscribe<TestMessage{{i}}>(OnTestMessage{{i}});
                                _hub.Unsubscribe<TestMessage0>(OnTestMessage0_{{i}});
                    """;

                file.WriteLine(txt);
            }

            // end
            txt =
                $$"""
                        }
                    }
                }
                """;
            file.WriteLine(txt);

        }



    }
}
