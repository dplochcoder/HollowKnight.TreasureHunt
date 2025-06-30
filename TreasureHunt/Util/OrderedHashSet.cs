using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;

namespace TreasureHunt.Util;

public class OrderedHashSet<T> : IEnumerable<T>
{
    private Dictionary<T, int> positions = [];
    private SortedDictionary<int, T> elements = [];
    private int nextIndex = 0;

    public List<T> JsonFormat
    {
        get => [.. elements.Values];
        set
        {
            Clear();
            foreach (var v in value) Add(v);
        }
    }

    public T Get(int index)
    {
        if (index < 0 || index >= positions.Count) throw new System.ArgumentOutOfRangeException($"{index}: [0, {positions.Count})");
        if (nextIndex > positions.Count)
        {
            List<T> elems = [.. this];
            Clear();
            foreach (var e in elems) Add(e);
        }

        return elements[index];
    }

    public bool Contains(T item) => positions.ContainsKey(item);

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

    public void Clear()
    {
        positions.Clear();
        elements.Clear();
        nextIndex = 0;
    }

    [JsonIgnore]
    public int Count => positions.Count;

    public IEnumerator<T> GetEnumerator() => elements.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => elements.Values.GetEnumerator();
}
