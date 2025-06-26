using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TreasureHunt;

public class LinkedDictionary<K, V> : IEnumerable<(K, V)>
{
    private LinkedHashSet<K> keys = [];
    private Dictionary<K, V> dict = [];

    public int Count => dict.Count;

    public void Add(K key, V value)
    {
        keys.Add(key);
        dict[key] = value;
    }

    public bool Contains(K key) => dict.ContainsKey(key);

    public bool TryGetValue(K key, out V value) => dict.TryGetValue(key, out value);

    public IEnumerable<K> Keys => keys;

    public IEnumerable<V> Values => keys.Select(k => dict[k]);

    public IEnumerator<(K, V)> GetEnumerator() => keys.Select(k => (k, dict[k])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => keys.Select(k => (k, dict[k])).GetEnumerator();
}
