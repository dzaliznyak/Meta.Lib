using System.Collections.Generic;

public class ReferenceCountedCollection<T>
{
    readonly Dictionary<T, int> _referenceCounts = new Dictionary<T, int>();

    public void Add(T item)
    {
        if (!_referenceCounts.ContainsKey(item))
        {
            _referenceCounts[item] = 1;
        }
        else
        {
            _referenceCounts[item]++;
        }
    }

    public void Remove(T item)
    {
        if (_referenceCounts.ContainsKey(item))
        {
            _referenceCounts[item]--;
            if (_referenceCounts[item] == 0)
            {
                _referenceCounts.Remove(item);
            }
        }
    }

    public IEnumerable<T> GetItems()
    {
        return _referenceCounts.Keys;
    }

    public int GetReferenceCount(T item)
    {
        return _referenceCounts.TryGetValue(item, out int count) ? count : 0;
    }
}
