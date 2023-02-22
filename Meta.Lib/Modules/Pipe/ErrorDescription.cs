namespace Meta.Lib.Modules.Pipe
{
    public class ErrorDescription
    {
        public ErrorDescription(string message, int errorCode = 0)
        {
            ErrorCode = errorCode;
            Message = message;
        }

        public int ErrorCode { get; }
        public string Message { get; }
    }
}
