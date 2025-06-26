using System.Collections;
using System.Collections.Generic;

namespace TreasureHunt;

public class LinkedHashSet<T> : IEnumerable<T>
{
    private List<T> list = [];
    private HashSet<T> set = [];

    public void Add(T item)
    {
        if (set.Add(item)) list.Add(item);
    }

    public IEnumerator<T> GetEnumerator() => list.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => list.GetEnumerator();
}
