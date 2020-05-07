using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Meta.Lib.Utils
{
    public class Split<T>
    {
        readonly List<Func<T, Task>> _outputs;

        public Split(List<Func<T, Task>> outputs)
        {
            _outputs = outputs;
        }

        public Task Put(T item)
        {
            var tasks = _outputs.Select(func => func(item)).ToList();
            return Task.WhenAll(tasks);
        }
    }
}
