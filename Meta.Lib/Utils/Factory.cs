using System;
using System.Threading.Tasks;

namespace Meta.Lib.Utils
{
    public class Factory<TKey, TParam, TObj>
    {
        readonly Func<TParam, TObj> _createDelegate;
        readonly Func<TKey, TObj, Task> _onAdded;
        readonly Func<TKey, Task> _onRemoved;

        public Factory(Func<TParam, TObj> createDelegate, Func<TKey, TObj, Task> onAdded, Func<TKey, Task> onRemoved)
        {
            _createDelegate = createDelegate;
            _onAdded = onAdded;
            _onRemoved = onRemoved;
        }

        /// <summary>
        /// Delegate to create an object
        /// </summary>
        /// <param name="key">Key for the Dictionary</param>
        /// <param name="prm">Parameter to pass into ctor of the creating object</param>
        /// <returns>created object</returns>
        public async Task<TObj> Create(TKey key, TParam prm)
        {
            var obj = _createDelegate(prm);
            await _onAdded(key, obj);
            return obj;
        }

        public async Task<bool> Remove(TKey key)
        {
            await _onRemoved(key);
            return true;
        }
    }
}
