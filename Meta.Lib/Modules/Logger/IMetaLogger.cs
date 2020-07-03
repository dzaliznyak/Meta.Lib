using System;

namespace Meta.Lib.Modules.Logger
{
    public interface IMetaLogger
    {
        string Name { get; }

        void Trace(string message);
        void Trace(Exception ex);
        void Trace(string message, Exception ex);

        void Debug(string message);
        void Debug(Exception ex);
        void Debug(string message, Exception ex);

        void Info(string message);
        void Info(Exception ex);
        void Info(string message, Exception ex);

        void Warning(string message);
        void Warning(Exception ex);
        void Warning(string message, Exception ex);

        void Error(string message);
        void Error(Exception ex);
        void Error(string message, Exception ex);

        void Critical(string message);
        void Critical(Exception ex);
        void Critical(string message, Exception ex);
    }
}