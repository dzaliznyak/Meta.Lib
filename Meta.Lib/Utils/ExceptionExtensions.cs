using System;

namespace Meta.Lib.Utils
{
    public static class ExceptionExtensions
    {
        // fixing the weird Microsoft's Message formatting e.g.:
        // "One or more errors occurred. (One or more errors occurred. (The operation has timed out.)) (The operation has timed out.)"
        // into this:
        // "One or more errors occurred. (The operation has timed out.)"
        public static AggregateException Fix(this AggregateException ex)
        {
            return new AggregateException(ex.Flatten().InnerExceptions);
        }
    }
}
