namespace E2E.Calendar.Core.Caching;

public sealed class LruCache<TKey, TValue>
    where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheEntry>> _entries;
    private readonly LinkedList<CacheEntry> _recency = new();
    private readonly object _gate = new();

    public LruCache(int capacity)
    {
        _capacity = Math.Max(1, capacity);
        _entries = new Dictionary<TKey, LinkedListNode<CacheEntry>>(_capacity);
    }

    public int Capacity => _capacity;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public bool TryGet(TKey key, out TValue value)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var node))
            {
                _recency.Remove(node);
                _recency.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
        }

        value = default!;
        return false;
    }

    public void Set(TKey key, TValue value)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                _recency.Remove(existing);
                _entries.Remove(key);
            }

            var node = new LinkedListNode<CacheEntry>(new CacheEntry(key, value));
            _recency.AddFirst(node);
            _entries[key] = node;

            while (_entries.Count > _capacity)
            {
                var evicted = _recency.Last;

                if (evicted is not null)
                {
                    _recency.RemoveLast();
                    _entries.Remove(evicted.Value.Key);
                }
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _recency.Clear();
        }
    }

    private sealed record CacheEntry(TKey Key, TValue Value);
}
