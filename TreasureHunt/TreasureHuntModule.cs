using ItemChanger;
using ItemChanger.Modules;
using RandomizerCore.Logic;
using RandomizerMod.IC;
using RandomizerMod.RC;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TreasureHunt;

internal class TreasureHuntModule : Module
{
    public RandomizationSettings Settings = new();
    public List<int> PlacementIndices = [];
    public HashSet<int> AcquiredPlacements = [];
    public int RevealedTo;

    private TrackerUI? ui;

    public override void Initialize()
    {
        RandoItemTag.AfterRandoItemGive += OnRandoItemGive;
        On.GameCompletionScreen.Start += OnGameCompletion;

        ui = new();
        UpdateRevealed();
    }

    public override void Unload()
    {
        RandoItemTag.AfterRandoItemGive -= OnRandoItemGive;
        On.GameCompletionScreen.Start -= OnGameCompletion;

        ui?.Destroy();
        ui = null;
    }

    private void UpdateRevealed()
    {
        List<int> toShow = [];
        for (int i = 0; i < PlacementIndices.Count; i++)
        {
            var placementIdx = PlacementIndices[i];
            if (toShow.Count == Settings.NumberOfReveals) break;
            if (i >= RevealedTo)
            {
                if (Settings.RollingWindow) ++RevealedTo;
                else if (toShow.Count == 0) RevealedTo = Math.Min(RevealedTo + Settings.NumberOfReveals, PlacementIndices.Count);
                else break;
            }
            if (AcquiredPlacements.Contains(placementIdx)) continue;

            toShow.Add(placementIdx);
        }

        ui?.Update(toShow, PlacementIndices.Count - AcquiredPlacements.Count);
    }

    private void OnRandoItemGive(int index, ReadOnlyGiveEventArgs args)
    {
        if (!PlacementIndices.Contains(index)) return;
        if (!AcquiredPlacements.Add(index)) return;

        UpdateRevealed();
    }

    private void OnGameCompletion(On.GameCompletionScreen.orig_Start orig, GameCompletionScreen self)
    {
        orig(self);

        ui?.Destroy();
        ui = null;
    }

    // Maps placement index -> progression sphere
    internal static Dictionary<int, int> CalculateProgressionSpheres(RandoModContext ctx)
    {
        LogicManager lm = new(new(ctx.LM));
        ProgressionManager pm = new(lm, ctx);
        var mu = pm.mu;

        mu.AddWaypoints(lm.Waypoints);
        mu.AddTransitions(lm.TransitionLookup.Values);
        mu.AddPlacements(ctx.Vanilla);
        if (ctx.transitionPlacements is not null) mu.AddEntries(ctx.transitionPlacements.Select(t => new PrePlacedItemUpdateEntry(t)));

        mu.StartUpdating();
        mu.SetLongTermRevertPoint();

        int sphere = 0;
        List<ItemPlacement> unclaimed = ctx.itemPlacements;
        Dictionary<int, int> spheres = [];

        while (unclaimed.Count > 0)
        {
            List<ItemPlacement> reachable = [];
            List<ItemPlacement> newUnclaimed = [];
            foreach (var placement in unclaimed)
            {
                if (placement.Location.CanGet(pm)) reachable.Add(placement);
                else newUnclaimed.Add(placement);
            }
            if (reachable.Count == 0) throw new ArgumentException("Seed is not completable");

            foreach (var placement in reachable)
            {
                pm.Add(placement.Item, placement.Location);
                spheres[placement.Index] = sphere;
            }

            unclaimed = newUnclaimed;
            sphere++;
        }

        return spheres;
    }

    private static int CompareItems(string name1, int sphere1, string name2, int sphere2)
    {
        if (sphere1 != sphere2) return sphere1.CompareTo(sphere2);
        else return RandomizationSettings.CompareItemNames(name1, name2);
    }

    internal void Start(RandoModContext ctx)
    {
        Dictionary<string, AbstractItem> placedItems = [];
        foreach (var item in ItemChanger.Internal.Ref.Settings.GetItems()) placedItems[item.name] = item;

        var spheres = CalculateProgressionSpheres(ctx);

        foreach (var placement in ctx.itemPlacements)
        {
            if (placement.Location.Name == LocationNames.Start) continue;
            if (!Settings.IsTrackedItem(placement.Item, placedItems)) continue;

            PlacementIndices.Add(placement.Index);
        }

        PlacementIndices.Sort((a, b) => CompareItems(ctx.itemPlacements[a].Item.Name, spheres[a], ctx.itemPlacements[b].Item.Name, spheres[b]));
        UpdateRevealed();
    }
}
