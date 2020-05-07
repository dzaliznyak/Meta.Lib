using System;
using System.Diagnostics;

namespace Meta.Lib.Modules.Logger
{
    /// <summary>
    /// Base class which can add logging capabilities to a derived class
    /// </summary>
    public abstract class LogWriterBase
    {
        protected readonly string _source;
        protected readonly IMetaLogger _log;

        protected LogWriterBase(string source, IMetaLogger log)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void WriteLine(string message, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Information)
        {
            try
            {
                _log.WriteLine(_source, message, severity);
            }
            catch
            {
                Debug.WriteLine(message);
            }
        }

        public void WriteLine(Exception ex, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Error)
        {
            try
            {
                _log.WriteLine(_source, ex, severity);
            }
            catch
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public void WriteLine(string message, Exception ex, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Error)
        {
            try
            {
                _log.WriteLine(_source, message, ex, severity);
            }
            catch
            {
                Debug.WriteLine(ex.Message);
            }
        }

        [Conditional("DEBUG")]
        public void WriteDebugLine(string message, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Debug)
        {
            _log.WriteDebugLine(_source, message, severity);
        }

        [Conditional("DEBUG")]
        public void WriteDebugLine(Exception ex, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Debug)
        {
            _log.WriteDebugLine(_source, ex, severity);
        }

        [Conditional("DEBUG")]
        public void WriteDebugLine(string message, Exception ex, MetaLogErrorSeverity severity = MetaLogErrorSeverity.Debug)
        {
            _log.WriteDebugLine(_source, message, ex, severity);
        }
    }
}
