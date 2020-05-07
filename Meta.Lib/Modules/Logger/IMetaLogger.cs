using System;

namespace Meta.Lib.Modules.Logger
{
    public interface IMetaLogger
    {
        void WriteLine(string source, string message, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Information, string stackTrace = null);
        void WriteLine(string source, Exception ex, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Error);
        void WriteLine(string source, string message, Exception ex, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Error);

        void WriteDebugLine(string source, string message, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Debug);
        void WriteDebugLine(string source, Exception ex, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Debug);
        void WriteDebugLine(string source, string message, Exception ex, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Debug);
    }
}