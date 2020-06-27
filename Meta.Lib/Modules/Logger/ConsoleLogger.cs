using System;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.Logger
{
    internal class ConsoleLogger
    {
        public Task WriteLine(MetaLogEntity item)
        {
            switch (item.Severity)
            {
                case MetaLogErrorSeverity.Critical:
                case MetaLogErrorSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case MetaLogErrorSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    break;
                case MetaLogErrorSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
                case MetaLogErrorSeverity.Debug:
                case MetaLogErrorSeverity.Trace:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }

            Console.WriteLine(item);
            return Task.CompletedTask;
        }
    }
}
