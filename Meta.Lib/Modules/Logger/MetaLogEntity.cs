using System;

namespace Meta.Lib.Modules.Logger
{
    public enum MetaLogErrorSeverity
    {
        None,
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }


    public class MetaLogEntity
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
        public MetaLogErrorSeverity Severity { get; set; }
        public string Source { get; set; }
        public string StackTrace { get; set; }

        public override string ToString()
        {
            return $"{Timestamp:HH:mm:ss.fff}\t{Source}\t{Severity}\t{Message}\t{StackTrace}";
        }
    }
}
