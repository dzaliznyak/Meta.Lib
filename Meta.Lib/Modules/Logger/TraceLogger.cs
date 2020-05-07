using System.Diagnostics;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.Logger
{
    internal class TraceLogger
    {
        public Task WriteLine(MetaLogEntity item)
        {
            Debug.WriteLine(item);
            return Task.CompletedTask;
        }
    }
}
