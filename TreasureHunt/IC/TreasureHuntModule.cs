using ItemChanger;
using ItemChanger.Items;
using ItemChanger.Modules;
using ItemChanger.Placements;
using ItemChanger.Tags;
using Modding;
using PurenailCore.SystemUtil;
using RandomizerCore.Extensions;
using RandomizerCore.Logic;
using RandomizerMod.IC;
using RandomizerMod.RC;
using System;
using System.Collections.Generic;
using System.Linq;
using TreasureHunt.Altar;
using TreasureHunt.Rando;
using TreasureHunt.Util;
using UnityEngine.SceneManagement;

namespace TreasureHunt.IC;

internal record DisplayData
{
    public List<int> treasures = [];
    public int treasuresRemaining;
    public List<int> cursed = [];
}

internal class TreasureHuntModule : Module
{
    public RandomizationSettings Settings = new();
    public int Seed;
    public List<int> PlacementIndices = [];
    public HashSet<int> AcquiredPlacements = [];
    public int RevealedTo;

    // Ritual data.
    public int CompletedRituals = 0;
    public float LastCompletedRitual;
    public List<int> CursedIndices = [];
    public float GameTime;

    private TrackerUI? ui;
    private CurseEffects? curseEffects;

    public override void Initialize()
    {
        DialogueUtil.Hook();
        RandoItemTag.AfterRandoItemGive += OnRandoItemGive;
        On.GameCompletionScreen.Start += OnGameCompletion;
        Events.AddSceneChangeEdit(SceneNames.RestingGrounds_02, MaybeSpawnAltar);
        ModHooks.HeroUpdateHook += UpdateGameTime;

        curseEffects = CurseEffects.Create();
        ui = new();
        UpdateDisplayData();
    }

    public override void Unload()
    {
        DialogueUtil.Unhook();
        RandoItemTag.AfterRandoItemGive -= OnRandoItemGive;
        On.GameCompletionScreen.Start -= OnGameCompletion;
        Events.RemoveSceneChangeEdit(SceneNames.RestingGrounds_02, MaybeSpawnAltar);
        ModHooks.HeroUpdateHook -= UpdateGameTime;

        ui?.Destroy();
        ui = null;
        if (curseEffects != null) UnityEngine.Object.Destroy(curseEffects.gameObject);
    }

    internal bool Finished() => AcquiredPlacements.Count >= PlacementIndices.Count;

    private DisplayData GetDisplayData()
    {
        List<int> treasures = [];
        for (int i = 0; i < PlacementIndices.Count; i++)
        {
            var placementIdx = PlacementIndices[i];
            if (treasures.Count == Settings.NumberOfReveals) break;
            if (i >= RevealedTo)
            {
                if (Settings.RollingWindow) ++RevealedTo;
                else if (treasures.Count == 0) RevealedTo = Math.Min(RevealedTo + Settings.NumberOfReveals, PlacementIndices.Count);
                else break;
            }
            if (AcquiredPlacements.Contains(placementIdx)) continue;

            treasures.Add(placementIdx);
        }

        return new()
        {
            treasures = treasures,
            treasuresRemaining = PlacementIndices.Count - AcquiredPlacements.Count,
            cursed = CursedIndices,
        };
    }

    internal static ProgressionManager NewEmptyPM(RandoModContext ctx)
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

        return pm;
    }

    internal string? GetArbitraryVisibleAccessibleTreasureName()
    {
        // Find if any of the visible treasures are accessible.
        var rs = RandomizerMod.RandomizerMod.RS;
        var ctx = rs.Context;
        var pm = rs.TrackerData.pm;

        foreach (var idx in GetVisibleTreasureIndices())
        {
            var placement = ctx.itemPlacements[idx];
            if (placement.Location.CanGet(pm)) return TrackerUI.Clean(placement.Location.Name);
        }

        return null;
    }

    internal bool IsCurseActive() => CursedIndices.Count > 0;

    internal List<int> GetVisibleTreasureIndices() => GetDisplayData().treasures;

    internal int GetRitualCost()
    {
        var min = 1200 + 1500 * CompletedRituals;
        var max = 1666 + 1500 * CompletedRituals;

        Random r = new(Seed + CompletedRituals * 17);
        return r.Next(max - min + 1) + min;
    }

    private bool CurseOfObsession(ReadOnlyGiveEventArgs args)
    {
        if (!Settings.CurseOfObsession) return false;
        if (args.Placement is EggShopPlacement) return false;
        if (args.Placement is ShopPlacement) return false;
        if (args.Item.GetTag<PersistentItemTag>() is PersistentItemTag tag && tag.Persistence != Persistence.Single) return false;
        if (args.Item is GrubItem || args.Item is MimicItem || args.Item is SpawnLumafliesItem) return false;

        return true;
    }

    internal void UpdateDisplayData()
    {
        ui?.Update(GetDisplayData());
        curseEffects?.SetCurseActive(IsCurseActive());
    }

    internal void RemoveCurse()
    {
        ++CompletedRituals;
        LastCompletedRitual = GameTime;
        AltarOfDivination.MaybeRestoreShade();
        curseEffects?.SetCurseActive(false);
    }

    private void OnRandoItemGive(int index, ReadOnlyGiveEventArgs args)
    {
        if (CursedIndices.Count > 0)
        {
            if (CursedIndices.Remove(index))
            {
                if (CursedIndices.Count == 0) RemoveCurse();
                UpdateDisplayData();
            }
            else if (PlacementIndices.Contains(index) && AcquiredPlacements.Add(index))
            {
                RemoveCurse();
                UpdateDisplayData();
            }
            else if (CurseOfObsession(args)) AltarOfDivination.QueueDirectDamage(2);
        }
        else if (PlacementIndices.Contains(index) && AcquiredPlacements.Add(index)) UpdateDisplayData();
    }

    private void OnGameCompletion(On.GameCompletionScreen.orig_Start orig, GameCompletionScreen self)
    {
        orig(self);

        ui?.Destroy();
        ui = null;
    }

    private void MaybeSpawnAltar(Scene scene)
    {
        if (!Settings.AltarOfDivination) return;
        AltarOfDivination.Spawn(scene);
    }

    private void UpdateGameTime() => GameManager.instance.IncreaseGameTimer(ref GameTime);

    // Maps placement index -> progression sphere
    internal static Dictionary<int, int> CalculateProgressionSpheres(RandoModContext ctx)
    {
        var pm = NewEmptyPM(ctx);

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
            if (reachable.Count == 0)
            {
                TreasureHuntMod.Instance!.LogError("Unreachable locations");
                foreach (var placement in newUnclaimed) TreasureHuntMod.Instance.LogError($"{placement.Item.Name} @ {placement.Location.Name}");
                TreasureHuntMod.Instance.LogError($"Progression Manager: {pm.Dump()}");
                throw new ArgumentException($"Seed is not completable; see ModLog.txt");
            }

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

    private int CompareItems(string name1, int sphere1, int random1, string name2, int sphere2, int random2)
    {
        if (sphere1 != sphere2) return sphere1.CompareTo(sphere2);
        else return Settings.TieBreaks switch {
            TieBreakerOrder.GoodItemsFirst => RandomizationSettings.CompareItemNames(name1, name2),
            TieBreakerOrder.GoodItemsLast => -RandomizationSettings.CompareItemNames(name1, name2),
            TieBreakerOrder.Random => random1.CompareTo(random2),
            _ => throw new ArgumentException($"Unknown order: {Settings.TieBreaks}")
        };  
    }

    internal void Start(RandoModContext ctx)
    {
        Seed = ctx.GenerationSettings.Seed;

        Dictionary<int, AbstractItem> placedItems = [];
        foreach (var item in ItemChanger.Internal.Ref.Settings.GetItems()) if (item.GetTag<RandoItemTag>() is RandoItemTag tag) placedItems[tag.id] = item;

        var spheres = CalculateProgressionSpheres(ctx);

        foreach (var placement in ctx.itemPlacements)
        {
            if (placement.Location.Name == LocationNames.Start) continue;
            if (!Settings.IsTrackedItem(placedItems[placement.Index])) continue;

            PlacementIndices.Add(placement.Index);
        }

        List<int> randomOrder = [];
        for (int i = 0; i < PlacementIndices.Count; i++) randomOrder.Add(i);
        randomOrder.Shuffle(new(ctx.GenerationSettings.Seed + 21));
        Dictionary<int, int> randomDict = [];
        for (int i = 0; i < PlacementIndices.Count; i++) randomDict.Add(PlacementIndices[i], randomOrder[i]);

        PlacementIndices.StableSort((a, b) => CompareItems(ctx.itemPlacements[a].Item.Name, spheres[a], randomDict[a], ctx.itemPlacements[b].Item.Name, spheres[b], randomDict[b]));
        UpdateDisplayData();
    }
}
