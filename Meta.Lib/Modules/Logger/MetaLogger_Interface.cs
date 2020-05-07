using System;
using System.Diagnostics;

namespace Meta.Lib.Modules.Logger
{
    public partial class MetaLogger : IMetaLogger
    {
        public void WriteLine(string source, string message, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Information, string stackTrace = null)
        {
            var item = new MetaLogEntity()
            {
                Source = source,
                Timestamp = DateTime.Now,
                Message = message,
                Severity = severity,
                StackTrace = stackTrace
            };

            Queue.Enqueue(item);
        }

        [Conditional("DEBUG")]
        void InternalWriteDebugLine(string source, string message, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Information, string stackTrace = null)
        {
            WriteLine(source, message, severity, stackTrace);
        }

        public void WriteLine(string source, Exception ex, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Error)
        {
            WriteLine(source, ex.Message, severity, ex.StackTrace);
        }

        public void WriteLine(string source, string message, Exception ex, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Error)
        {
            WriteLine(source, $"{message} ({ex.Message})", severity, ex.StackTrace);
        }

        public void WriteDebugLine(string source, string message, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Debug)
        {
            InternalWriteDebugLine(source, message, severity);
        }

        public void WriteDebugLine(string source, Exception ex, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Debug)
        {
            InternalWriteDebugLine(source, ex.Message, severity);
        }

        public void WriteDebugLine(string source, string message, Exception ex, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Debug)
        {
            InternalWriteDebugLine(source, $"{message} ({ex.Message})", severity, ex.StackTrace);
        }
    }
}
