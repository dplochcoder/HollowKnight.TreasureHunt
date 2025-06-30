using RandomizerCore;
using RandomizerCore.Logic;
using RandomizerMod.RC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TreasureHunt.IC;
using TreasureHunt.Rando;
using TreasureHunt.Util;

namespace TreasureHunt.Altar;

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
        thread.Start();
    }

    internal void Abort() => thread.Abort();

    internal List<int>? GetResult()
    {
        lock (this) { return result; }
    }

    private List<int>? result;

    private static readonly HashSet<string> HIGH_VOLUME_TERMS = ["ESSENCE", "GEO", "GRUBS", "HALLOWNESTSEALS", "KINGSIDOLS", "MAPS", "RANCIDEGGS", "SIMPLE", "WANDERERSJOURNALS"];

    private static (Term, int)? GetSingleTermIncrease(ProgressionManager pm, ProgressionData before, ILogicItem item, out bool isHighVolume)
    {
        pm.StartTemp();
        item.AddTo(pm);
        var after = pm.GetSnapshot();
        pm.RemoveTempItems();

        List<Term> terms = [.. ProgressionData.GetDiffTerms(before, after)];
        isHighVolume = terms.Any(t => HIGH_VOLUME_TERMS.Contains(t.Name));
        if (terms.Count != 1 || terms[0].Type == TermType.State) return null;

        var term = terms[0];
        return (term, after.GetValue(term) - before.GetValue(term));
    }

    // Sort lesser values before larger ones. This is mostly relevant for essence drops.
    private static int CompareTerms(ProgressionManager pm, ProgressionData before, ItemPlacement a, ItemPlacement b)
    {
        var p1 = GetSingleTermIncrease(pm, before, a.Item, out var p1HighVol);
        var p2 = GetSingleTermIncrease(pm, before, b.Item, out var p2HighVol);
        if (p1HighVol != p2HighVol) return p1HighVol ? -1 : 1;

        if (p1 == null && p2 == null) return a.Index.CompareTo(b.Index);
        if (p1 == null) return -1;
        if (p2 == null) return 1;

        var (term1, value1) = p1.Value;
        var (term2, value2) = p2.Value;
        if (term1 != term2) return term1.Id.CompareTo(term2.Id);
        else return value1.CompareTo(value2);
    }

    private List<int> SearchImpl()
    {
        var time = DateTime.Now;
        string StatTime()
        {
            var newTime = DateTime.Now;
            string ret = $"{(newTime - time).Seconds:0.000} Seconds";
            time = newTime;
            return ret;
        }

        var mu = pm.mu;

        // Add all obtained items.
        mu.StopUpdating();
        foreach (var obtained in obtainedItems)
        {
            var placement = itemPlacements[obtained];
            pm.Add(placement.Item, placement.Location);
        }
        mu.StartUpdating();

        bool CanGetAnyTarget() => targets.Select(t => itemPlacements[t].Location).Any(l => l.CanGet(pm));

        // First, check if any singular item would solve the problem.
        int singleItems = 0;
        foreach (var placement in itemPlacements)
        {
            if (obtainedItems.Contains(placement.Index) || !placement.Location.CanGet(pm)) continue;

            pm.StartTemp();
            pm.Add(placement.Item, placement.Location);

            if (CanGetAnyTarget())
            {
                TreasureHuntMod.Instance!.Log($"ALTAR: Found single item solution: {placement.Location.Name} in {StatTime()}");
                return [placement.Index];
            }
            else
            {
                pm.RemoveTempItems();
                ++singleItems;
            }
        }
        TreasureHuntMod.Instance!.Log($"ALTAR: Rejected {singleItems} single item solutions in {StatTime()}");

        // Grab full progression spheres until a treasure is available, or we collect too many items.
        LinkedHashSet<int> newObtained = [];
        pm.StartTemp();

        int spheres = 0;
        List<ItemPlacement> unreachable = [.. itemPlacements.Where(p => !obtainedItems.Contains(p.Index))];
        for (int i = 0; i < RandomizationSettings.MAX_CURSES; i++)
        {
            List<ItemPlacement> reachable = [];
            List<ItemPlacement> newUnreachable = [];
            foreach (var placement in unreachable)
            {
                if (placement.Location.CanGet(pm))
                {
                    reachable.Add(placement);
                    newObtained.Add(placement.Index);
                }
                else newUnreachable.Add(placement);
            }

            mu.StopUpdating();
            foreach (var p in reachable) pm.Add(p.Item, p.Location);
            mu.StartUpdating();

            ++spheres;
            if (CanGetAnyTarget()) break;
            unreachable = newUnreachable;
        }
        TreasureHuntMod.Instance.Log($"ALTAR: Searched {spheres} progression spheres in {StatTime()}");
        if (!CanGetAnyTarget())
        {
            TreasureHuntMod.Instance.Log($"ALTAR: Too many progression spheres.");
            return [];
        }
        pm.RemoveTempItems();

        bool CanReachTarget(Func<ItemPlacement, bool> filter)
        {
            pm.StartTemp();

            List<ItemPlacement> unreachable = [.. itemPlacements.Where(filter)];
            while (true)
            {
                List<ItemPlacement> reachable = [];
                List<ItemPlacement> newUnreachable = [];
                foreach (var placement in unreachable)
                {
                    if (placement.Location.CanGet(pm)) reachable.Add(placement);
                    else newUnreachable.Add(placement);
                }
                if (reachable.Count == 0)
                {
                    pm.RemoveTempItems();
                    return false;
                }

                mu.StopUpdating();
                foreach (var p in reachable) pm.Add(p.Item, p.Location);
                mu.StartUpdating();

                if (CanGetAnyTarget())
                {
                    pm.RemoveTempItems();
                    return true;
                }

                unreachable = newUnreachable;
            }
        }

        var tempPm = TreasureHuntModule.NewEmptyPM(RandomizerMod.RandomizerMod.RS.Context);
        var beforeSnapshot = tempPm.GetSnapshot();

        List<int> toPrune = [.. newObtained];
        toPrune.Sort((a, b) => CompareTerms(tempPm, beforeSnapshot, itemPlacements[a], itemPlacements[b]));
        toPrune.Reverse();

        int pruned = 0;
        foreach (var idx in toPrune)
        {
            if (CanReachTarget(p => newObtained.Contains(p.Index) && p.Index != idx))
            {
                newObtained.Remove(idx);
                pruned++;
            }
        }
        TreasureHuntMod.Instance.Log($"ALTAR: Pruned {pruned} of {toPrune.Count} items in {StatTime()}");

        if (newObtained.Count <= RandomizationSettings.MAX_CURSES)
        {
            TreasureHuntMod.Instance.Log($"ALTAR: Success! Found {newObtained.Count} item chain.");
            return [.. newObtained];
        }
        else
        {
            TreasureHuntMod.Instance.Log($"ALTAR: Failure; chain too long ({newObtained.Count} > {RandomizationSettings.MAX_CURSES})");
            return [];
        }
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
