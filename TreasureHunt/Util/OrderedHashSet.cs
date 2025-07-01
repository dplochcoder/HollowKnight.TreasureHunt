using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;

namespace TreasureHunt.Util;

public class OrderedHashSet<T> : IList<T>
{
    private Dictionary<T, int> positions = [];
    private SortedDictionary<int, T> elements = [];
    private int nextIndex = 0;

    private void Compact()
    {
        if (nextIndex == positions.Count) return;

        List<T> elems = [.. this];
        Clear();
        foreach (var e in elems) Add(e);
    }

    public T Get(int index)
    {
        if (index < 0 || index >= positions.Count) throw new System.ArgumentOutOfRangeException($"{index}: [0, {positions.Count})");

        Compact();
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

    [JsonIgnore]
    public bool IsReadOnly => false;

    public T this[int index] { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

    public IEnumerator<T> GetEnumerator() => elements.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => elements.Values.GetEnumerator();

    public int IndexOf(T item)
    {
        Compact();
        return positions.TryGetValue(item, out var idx) ? idx : -1;
    }

    private void ModifyList(System.Action<List<T>> action)
    {
        List<T> list = [.. this];
        action(list);
        Clear();
        list.ForEach(i => Add(i));
    }

    public void Insert(int index, T item)
    {
        if (Contains(item)) return;
        ModifyList(l => l.Insert(index, item));
    }

    public void RemoveAt(int index)
    {
        Compact();
        positions.Remove(elements[index]);
        elements.Remove(index);
    }

    void ICollection<T>.Add(T item) => Add(item);

    public void CopyTo(T[] array, int arrayIndex) 
    {
        List<T> list = [.. this];
        list.CopyTo(array, arrayIndex);
    }
}
