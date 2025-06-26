using RandomizerCore.Collections;
using RandomizerCore.Logic;
using RandomizerCore.Logic.StateLogic;
using RandomizerMod.RC;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TreasureHunt;

internal class ProgressionKey
{
    private readonly Dictionary<Term, int> changedInts = [];
    private readonly Dictionary<Term, StateUnion?> changedStates = [];

    internal ProgressionKey() { }

    internal ProgressionKey(ProgressionData deltaBase, ProgressionData data)
    {
        foreach (Term t in ProgressionData.GetDiffTerms(deltaBase, data))
        {
            switch (t.Type)
            {
                case TermType.Int:
                case TermType.SignedByte:
                    changedInts.Add(t, data.GetValue(t));
                    break;
                case TermType.State:
                    changedStates.Add(t, data.GetState(t));
                    break;
            }
        }
    }

    private static int Hash(RCBitArray arr)
    {
        int hash = arr.Length ^ 0x5CED46F8;
        foreach (var v in arr) hash = hash * (0x5CED46F3 + (v ? 1 : 0)) + 0x38E3229F;
        return hash;
    }

    private static int Hash(int[] arr)
    {
        int hash = arr.Length ^ 0x5CED46F8;
        foreach (var v in arr) hash ^= v + 0x38E3229F;
        return hash;
    }

    private static int Hash(State state)
    {
        int hash = Hash(state.CloneBools()) ^ 0x68281862;
        hash ^= Hash(state.CloneInts()) ^ 0x2E041130;
        return hash;
    }

    private static int Hash(StateUnion? union)
    {
        if (union == null) return 0;

        int hash = union.Count ^ 0x4046DC55;
        foreach (var state in union) hash += Hash(state);
        return hash;
    }

    public override bool Equals(object obj)
    {
        if (this == obj) return true;
        if (obj is not ProgressionKey key) return false;
        if (!DictExtensions.Equal(changedInts, key.changedInts, (i1, i2) => i1 == i2)) return false;
        if (!DictExtensions.Equal(changedStates, key.changedStates, StateUnion.IsProgressivelyEqual)) return false;

        return true;
    }

    public override int GetHashCode()
    {
        int hash = 0x2FAA0773;
        hash ^= DictExtensions.DictHash(changedInts, i => i) + 0x3B660301;
        hash ^= DictExtensions.DictHash(changedStates, Hash) + 0x7C94AC39;
        return hash;
    }
}

// An immutable list which is order-preserving, but is unordered with respect to hash/equals.
internal class UnorderedList : IEnumerable<int>
{
    private readonly int[] indices;

    internal UnorderedList() => indices = [];
    private UnorderedList(int[] indices) => this.indices = indices;

    public int Length => indices.Length;

    public int this[int index] => indices[index];

    public IEnumerator<int> GetEnumerator() => indices.AsEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => indices.GetEnumerator();

    internal UnorderedList Add(int idx)
    {
        if (indices.Contains(idx)) return this;

        int[] next = new int[indices.Length + 1];
        Array.Copy(indices, next, indices.Length);
        next[indices.Length] = idx;
        return [.. next];
    }

    public override bool Equals(object obj)
    {
        if (obj is not UnorderedList list) return false;

        if (indices.Length != list.indices.Length) return false;
        HashSet<int> set = [.. indices];
        return list.indices.All(set.Contains);
    }

    public override int GetHashCode()
    {
        int hash = indices.Length ^ 0x5CED46F8;
        foreach (var v in indices) hash += v ^ 0x38E3229F;
        return hash;
    }
}

internal class SearchAlgorithm
{
    private readonly List<int> targets;
    private readonly HashSet<int> obtainedItems;
    private readonly List<ItemPlacement> itemPlacements;
    private readonly ProgressionManager pm;
    private readonly Thread thread;

    internal SearchAlgorithm(List<int> targets)
    {
        var rs = RandomizerMod.RandomizerMod.RS;
        var ctx = rs.Context;

        this.targets = targets;
        obtainedItems = [.. rs.TrackerData.obtainedItems];
        itemPlacements = [.. ctx.itemPlacements];
        pm = TreasureHuntModule.NewEmptyPM(ctx);

        thread = new(Search);
        thread.Priority = ThreadPriority.BelowNormal;
        thread.Start();
    }

    internal List<int>? GetResult()
    {
        lock (this) { return result; }
    }

    private List<int>? result;

    private List<int> SearchImpl()
    {
        var mu = pm.mu;

        // Add all obtained items.
        mu.StopUpdating();
        foreach (var obtained in obtainedItems)
        {
            var placement = itemPlacements[obtained];
            pm.Add(placement.Item, placement.Location);
        }
        mu.StartUpdating();
        var baseSnapshot = pm.GetSnapshot();

        List<int> relevant = [];
        foreach (var placement in itemPlacements)
        {
            if (obtainedItems.Contains(placement.Index)) continue;
            if (!placement.Item.GetAffectedTerms().GetEnumerator().MoveNext()) continue;
            
            relevant.Add(placement.Index);
        }

        HashSet<UnorderedList> visited = [[]];
        LinkedDictionary<ProgressionKey, UnorderedList> progression = [];
        progression.Add(new(), []);

        while (progression.Count > 0)
        {
            LinkedHashSet<UnorderedList> newLists = [];
            foreach (var prevList in visited)
            {
                foreach (var index in relevant)
                {
                    var newList = prevList.Add(index);
                    if (newList.Length <= RandomizationSettings.MAX_CURSES && visited.Add(newList)) newLists.Add(newList);
                }
            }

            LinkedDictionary<ProgressionKey, UnorderedList> newProgression = [];
            foreach (var newList in newLists)
            {
                pm.StartTemp();
                for (int i = 0; i < newList.Length - 1; i++)
                {
                    // We dont' check location logic for these since they were verified in earlier loops.
                    var placement = itemPlacements[newList[i]];
                    pm.Add(placement.Item, placement.Location);
                }

                var lastPlacement = itemPlacements[newList[newList.Length - 1]];
                if (!lastPlacement.Location.CanGet(pm))
                {
                    // We can't obtain this item yet.
                    pm.RemoveTempItems();
                    continue;
                }
                pm.Add(lastPlacement.Item, lastPlacement.Location);

                ProgressionKey key = new(baseSnapshot, pm.GetSnapshot());
                if (progression.Contains(key))
                {
                    // This item doesn't give us any new progression.
                    pm.RemoveTempItems();
                    continue;
                }

                // Check if we're done.
                if (targets.Any(t => itemPlacements[t].Location.CanGet(pm))) return [.. newList];

                // Go to next iteration.
                newProgression.Add(key, newList);
                pm.RemoveTempItems();
            }

            progression = newProgression;
        }

        return [];
    }

    private void Search()
    {
        try
        {
            var tmp = SearchImpl();
            lock (this) { result = tmp; }
        }
        catch (Exception e)
        {
            TreasureHuntMod.Instance!.LogError($"Search failed: {e}");
            lock (this) { result = []; }
        }
    }
}

internal static class DictExtensions
{
    internal static bool Equal<K, V>(Dictionary<K, V> left, Dictionary<K, V> right, Func<V, V, bool> comparer)
    {
        if (left.Count != right.Count) return false;
        foreach (var e in left)
        {
            var v1 = e.Value;
            if (!right.TryGetValue(e.Key, out var v2)) return false;
            if (!comparer(v1, v2)) return false;
        }
        return true;
    }

    internal static int DictHash<K, V>(Dictionary<K, V> dict, Func<V, int> hasher)
    {
        int hash = dict.Count ^ 0x5B256441;
        foreach (var e in dict) hash += (e.Key!.GetHashCode() ^ hasher(e.Value)) ^ 0x5123F416;
        return hash;
    }
}