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

    public class PipeMessageEventArgs : EventArgs
    {

        public Guid CorrelationId { get; set; }
        public PayloadType PayloadType { get; }
        public byte[] Data { get; }
        public object Obj { get; }
        public string Str { get; }
        public ErrorDescription Error { get; }

        public PipeMessageEventArgs(byte[] data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            PayloadType = PayloadType.ByteArray;
        }

        public PipeMessageEventArgs(object obj)
        {
            Obj = obj ?? throw new ArgumentNullException(nameof(obj));
            PayloadType = PayloadType.Object;
        }

        public PipeMessageEventArgs(string str)
        {
            Str = str ?? throw new ArgumentNullException(nameof(str));
            PayloadType = PayloadType.String;
        }

        public PipeMessageEventArgs(ErrorDescription error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
            PayloadType = PayloadType.Error;
        }
    }
}
