using Meta.Lib.Modules.Logger;
using NLog;
using System;

namespace TestServer
{
    public class NLogAdapter : IMetaLogger
    {
        readonly Logger _log;

        public string Name => _log.Name;

        public NLogAdapter(Logger log)
        {
            _log = log;
        }

        //LogLevel GetLogLevel(MetaLogErrorSeverity severity)
        //{
        //    return severity switch
        //    {
        //        MetaLogErrorSeverity.Critical => LogLevel.Fatal,
        //        MetaLogErrorSeverity.Error => LogLevel.Error,
        //        MetaLogErrorSeverity.Warning => LogLevel.Warn,
        //        MetaLogErrorSeverity.Information => LogLevel.Info,
        //        MetaLogErrorSeverity.Debug => LogLevel.Debug,
        //        MetaLogErrorSeverity.Trace => LogLevel.Trace,
        //        MetaLogErrorSeverity.None => LogLevel.Off,
        //        _ => LogLevel.Debug,
        //    };
        //}

        // Trace
        public void Trace(string message)
        {
            _log.Trace(message);
        }

        public void Trace(Exception ex)
        {
            _log.Trace(ex);
        }

        public void Trace(string message, Exception ex)
        {
            _log.Trace(ex, message);
        }


        // Debug
        public void Debug(string message)
        {
            _log.ConditionalDebug(message);
        }

        public void Debug(Exception ex)
        {
            _log.ConditionalDebug(ex);
        }

        public void Debug(string message, Exception ex)
        {
            _log.ConditionalDebug(ex, message);
        }


        // Info
        public void Info(string message)
        {
            _log.Info(message);
        }

        public void Info(Exception ex)
        {
            _log.Info(ex);
        }

        public void Info(string message, Exception ex)
        {
            _log.Info(ex, message);
        }


        // Warning
        public void Warning(string message)
        {
            _log.Warn(message);
        }

        public void Warning(Exception ex)
        {
            _log.Warn(ex);
        }

        public void Warning(string message, Exception ex)
        {
            _log.Warn(ex, message);
        }


        // Error
        public void Error(string message)
        {
            _log.Error(message);
        }

        public void Error(Exception ex)
        {
            _log.Error(ex);
        }

        public void Error(string message, Exception ex)
        {
            _log.Error(ex, message);
        }


        // Critical
        public void Critical(string message)
        {
            _log.Fatal(message);
        }

        public void Critical(Exception ex)
        {
            _log.Fatal(ex);
        }

        public void Critical(string message, Exception ex)
        {
            _log.Fatal(ex, message);
        }

    }
}
