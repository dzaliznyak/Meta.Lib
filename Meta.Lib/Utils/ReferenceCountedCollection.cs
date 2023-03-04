using System.Collections.Generic;

namespace Meta.Lib.Utils
{
    public class ReferenceCountedCollection<T>
    {
        readonly Dictionary<T, int> _dict = new Dictionary<T, int>();

        public bool Add(T item)
        {
            if (!_dict.ContainsKey(item))
            {
                _dict[item] = 1;
                return true;
            }
            else
            {
                _dict[item]++;
                return false;
            }
        }

        public bool Remove(T item)
        {
            if (_dict.ContainsKey(item))
            {
                _dict[item]--;
                if (_dict[item] == 0)
                {
                    _dict.Remove(item);
                    return true;
                }
            }
            return false;
        }

        public IEnumerable<T> GetItems()
        {
            return _dict.Keys;
        }

        public int GetReferenceCount(T item)
        {
            return _dict.TryGetValue(item, out int count) ? count : 0;
        }

        public void Clear()
        {
            _dict.Clear();
        }
    }
}