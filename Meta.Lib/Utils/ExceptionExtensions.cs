using System;

namespace Meta.Lib.Utils
{
    public static class ExceptionExtensions
    {

        /// <summary>
        /// Fixing the weird Microsoft's Message formatting e.g.:
        /// "One or more errors occurred. (One or more errors occurred. (The operation has timed out.)) (The operation has timed out.)"
        /// into this:
        /// "One or more errors occurred. (The operation has timed out.)"
        /// </summary>
        public static AggregateException Fix(this AggregateException ex)
        {
            return new AggregateException(ex.Flatten().InnerExceptions);
        }
    }
}
