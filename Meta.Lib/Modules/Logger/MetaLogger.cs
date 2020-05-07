using Meta.Lib.Collections;
using Meta.Lib.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.Logger
{
    public partial class MetaLogger : IMetaLogger
    {
        internal ManyToOneQueue<MetaLogEntity> Queue { get; }
        internal Split<MetaLogEntity> Split { get; }
        internal TraceLogger TraceLogger { get; }
        internal ConsoleLogger ConsoleLogger { get; }
        public MetaLogger()
        {
            Queue = new ManyToOneQueue<MetaLogEntity>(Split_Put);
            var splitOutputs = new List<Func<MetaLogEntity, Task>>
            {
                TraceLogger_WriteLine,
                ConsoleLogger_WriteLine,
            };
            Split = new Split<MetaLogEntity>(splitOutputs);
            TraceLogger = new TraceLogger();
            ConsoleLogger = new ConsoleLogger();
        }

        Task Split_Put(MetaLogEntity arg0)
        {
            return Split.Put(arg0);
        }

        Task TraceLogger_WriteLine(MetaLogEntity arg0)
        {
            return TraceLogger.WriteLine(arg0);
        }

        Task ConsoleLogger_WriteLine(MetaLogEntity arg0)
        {
            return ConsoleLogger.WriteLine(arg0);
        }

    }
}
