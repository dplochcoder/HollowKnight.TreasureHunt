using System.Collections;
using System.Collections.Generic;

namespace TreasureHunt;

public class LinkedHashSet<T> : IEnumerable<T>
{
    private Dictionary<T, int> positions = [];
    private SortedDictionary<int, T> elements = [];

    private int nextIndex = 0;

    public bool Add(T item)
    {
        if (positions.ContainsKey(item)) return false;

        positions.Add(item, nextIndex);
        elements.Add(nextIndex, item);
        nextIndex++;
        return true;
    }

    public bool Remove(T item)
    {
        if (!positions.TryGetValue(item, out var idx)) return false;

        positions.Remove(item);
        elements.Remove(idx);
        return true;
    }

    public int Count => positions.Count;

    public IEnumerator<T> GetEnumerator() => elements.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => elements.Values.GetEnumerator();
}
