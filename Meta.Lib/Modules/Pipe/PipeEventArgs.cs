using System;

namespace Meta.Lib.Modules.Pipe
{
    public class PipeConnectionEventArgs : EventArgs
    {
        public MetaPipeConnection Connection { get; }

        public PipeConnectionEventArgs(MetaPipeConnection connection)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }
    }

    public class PipeConnectionMessageEventArgs : EventArgs
    {
        public byte[] Data { get; }

        public PipeConnectionMessageEventArgs(byte[] data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }
    }
}
