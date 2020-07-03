using System;
using System.Diagnostics;

namespace Meta.Lib.Modules.Logger
{
    public partial class MetaLogger : IMetaLogger
    {
        public static IMetaLogger Default = new MetaLogger();

        public string Name { get; set; }

        void Log(string message, MetaLogErrorSeverity severity, string stackTrace = null)
        {
            var item = new MetaLogEntity()
            {
                Source = this.Name,
                Timestamp = DateTime.Now,
                Message = message,
                Severity = severity,
                StackTrace = stackTrace
            };

            Queue.Enqueue(item);
        }

        [Conditional("DEBUG")]
        void DebugLog(string message, MetaLogErrorSeverity severity, string stackTrace = null)
        {
            var item = new MetaLogEntity()
            {
                Source = this.Name,
                Timestamp = DateTime.Now,
                Message = message,
                Severity = severity,
#if DEBUG
                StackTrace = stackTrace
#endif
            };

            Queue.Enqueue(item);
        }


        // Trace
        public void Trace(string message)
        {
            Log(message, MetaLogErrorSeverity.Trace);
        }

        public void Trace(Exception ex)
        {
            Log(ex.Message, MetaLogErrorSeverity.Trace, ex.StackTrace);
        }

        public void Trace(string message, Exception ex)
        {
            Log($"{message} ({ex.Message})", MetaLogErrorSeverity.Trace, ex.StackTrace);
        }


        // Debug
        public void Debug(string message)
        {
            DebugLog(message, MetaLogErrorSeverity.Debug);
        }

        public void Debug(Exception ex)
        {
            DebugLog(ex.Message, MetaLogErrorSeverity.Debug, ex.StackTrace);
        }

        public void Debug(string message, Exception ex)
        {
            DebugLog($"{message} ({ex.Message})", MetaLogErrorSeverity.Debug, ex.StackTrace);
        }


        // Information
        public void Info(string message)
        {
            Log(message, MetaLogErrorSeverity.Info);
        }

        public void Info(Exception ex)
        {
            Log(ex.Message, MetaLogErrorSeverity.Info, ex.StackTrace);
        }

        public void Info(string message, Exception ex)
        {
            Log($"{message} ({ex.Message})", MetaLogErrorSeverity.Info, ex.StackTrace);
        }


        // Warning
        public void Warning(string message)
        {
            Log(message, MetaLogErrorSeverity.Warning);
        }

        public void Warning(Exception ex)
        {
            Log(ex.Message, MetaLogErrorSeverity.Warning, ex.StackTrace);
        }

        public void Warning(string message, Exception ex)
        {
            Log($"{message} ({ex.Message})", MetaLogErrorSeverity.Warning, ex.StackTrace);
        }


        // Error
        public void Error(string message)
        {
            Log(message, MetaLogErrorSeverity.Error);
        }

        public void Error(Exception ex)
        {
            Log(ex.Message, MetaLogErrorSeverity.Error, ex.StackTrace);
        }

        public void Error(string message, Exception ex)
        {
            Log($"{message} ({ex.Message})", MetaLogErrorSeverity.Error, ex.StackTrace);
        }


        // Critical
        public void Critical(string message)
        {
            Log(message, MetaLogErrorSeverity.Critical);
        }

        public void Critical(Exception ex)
        {
            Log(ex.Message, MetaLogErrorSeverity.Critical, ex.StackTrace);
        }

        public void Critical(string message, Exception ex)
        {
            Log($"{message} ({ex.Message})", MetaLogErrorSeverity.Critical, ex.StackTrace);
        }
    }
}
