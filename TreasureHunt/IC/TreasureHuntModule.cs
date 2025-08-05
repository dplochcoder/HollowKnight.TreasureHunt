using ItemChanger;
using ItemChanger.Items;
using ItemChanger.Modules;
using ItemChanger.Placements;
using ItemChanger.Tags;
using Modding;
using PurenailCore.CollectionUtil;
using PurenailCore.SystemUtil;
using RandomizerCore.Extensions;
using RandomizerCore.Logic;
using RandomizerMod.IC;
using RandomizerMod.RC;
using System;
using System.Collections.Generic;
using System.Linq;
using TreasureHunt.Altar;
using TreasureHunt.Interop;
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

internal record Curse
{
    public int Index;
    public HashSet<int> TreasuresBefore = [];
    public List<int> CurseItems = [];

    public void Merge(Curse other)
    {
        if (Index != other.Index) throw new ArgumentException($"Curse index mismatch: {Index} != {other.Index}");

        other.TreasuresBefore.ForEach(i => TreasuresBefore.Add(i));

        CurseItems.AddRange(other.CurseItems);
        HashSet<int> unique = [.. CurseItems];
        CurseItems = [.. unique];
        CurseItems.Sort();
    }
}

internal class TreasureHuntModule : Module
{
    public RandomizationSettings Settings = new();
    public int Seed;
    public HashSet<int> Treasures = [];
    public IndexedSet<int> RemainingTreasures = [];
    public HashSet<int> Acquired = [];

    // Ritual data.
    public List<Curse> Curses = [];
    public float LastLiftedCurse;
    public float GameTime;

    private TrackerUI? ui;
    private CurseEffects? curseEffects;

    private static TreasureHuntModule? instance;

    internal static TreasureHuntModule? Get() => instance;

    public override void Initialize()
    {
        instance = this;

        DialogueUtil.Hook();
        RandoItemTag.AfterRandoItemGive += OnRandoItemGive;
        On.GameCompletionScreen.Start += OnGameCompletion;
        Events.AddSceneChangeEdit(SceneNames.RestingGrounds_02, MaybeSpawnAltar);
        ModHooks.HeroUpdateHook += UpdateGameTime;
        if (ModHooks.GetMod("ItemSyncMod") is Mod) HookItemSync();

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
        if (ModHooks.GetMod("ItemSyncMod") is Mod) UnhookItemSync();

        ui?.Destroy();
        ui = null;
        if (curseEffects != null) UnityEngine.Object.Destroy(curseEffects.gameObject);

        instance = null;
    }

    private void HookItemSync() => ItemSyncInterop.HookItemSync();

    private void UnhookItemSync() => ItemSyncInterop.UnhookItemSync();

    internal bool Finished() => RemainingTreasures.Count == 0;

    private List<int> ApplicableCurseItems(Curse curse)
    {
        if (Treasures.Any(i => Acquired.Contains(i) && !curse.TreasuresBefore.Contains(i))) return [];
        else return [.. curse.CurseItems.Where(i => !Acquired.Contains(i))];
    }

    private bool isCursed;
    private List<int> UpdateCurseItems()
    {
        bool prev = isCursed;
        HashSet<int> indices = [.. Curses.SelectMany(ApplicableCurseItems)];
        isCursed = indices.Count > 0;
        if (prev && !isCursed)
        {
            LastLiftedCurse = GameTime;
            AltarOfDivination.MaybeRestoreShade();
        }

        List<int> sorted = [.. indices];
        sorted.Sort();
        return sorted;
    }

    private void MaybeSendCurse(Curse curse) => ItemSyncInterop.MaybeSendCurse(curse);

    internal void GrantCurse(List<int> curseItems)
    {
        Curse curse = new()
        {
            Index = Curses.Count,
            TreasuresBefore = [.. Treasures.Where(Acquired.Contains)],
            CurseItems = [.. curseItems]
        };

        ReceiveCurse(curse);
        if (ModHooks.GetMod("ItemSyncMod") is Mod) MaybeSendCurse(curse);
    }

    internal void ReceiveCurse(Curse curse)
    {
        while (Curses.Count < curse.Index) Curses.Add(new() { Index = Curses.Count });
        if (curse.Index == Curses.Count) Curses.Add(curse);
        else Curses[curse.Index].Merge(curse);

        GameManager.instance.SaveGame();
        UpdateDisplayData();
    }

    private DisplayData GetDisplayData() => new()
    {
        treasures = [.. RemainingTreasures.Take(Settings.NumberOfReveals)],
        treasuresRemaining = RemainingTreasures.Count,
        cursed = UpdateCurseItems()
    };

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

    internal bool IsCurseActive() => UpdateCurseItems().Count > 0;

    internal int CompletedRituals() => Curses.Where(c => ApplicableCurseItems(c).Count == 0).Count();

    internal List<int> GetVisibleTreasureIndices() => GetDisplayData().treasures;

    internal int GetRitualCost()
    {
        int rituals = CompletedRituals();
        var min = 1200 + 1500 * rituals;
        var max = 1666 + 1500 * rituals;

        Random r = new(Seed + rituals * 17);
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

    internal void UpdateDisplayData(DisplayData? data = null)
    {
        data ??= GetDisplayData();
        ui?.Update(data);
        curseEffects?.SetCurseActive(data.cursed.Count > 0);
    }

    private void OnRandoItemGive(int index, ReadOnlyGiveEventArgs args)
    {
        if (!Acquired.Add(index)) return;

        if (!RemainingTreasures.Remove(index))
        {
            var data = GetDisplayData();
            if (data.cursed.Count > 0 && !data.cursed.Contains(index) && CurseOfObsession(args)) AltarOfDivination.QueueDirectDamage(2);
            UpdateDisplayData(data);
        }
        else UpdateDisplayData();
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

        List<int> treasures = [];
        foreach (var placement in ctx.itemPlacements)
        {
            if (placement.Location.Name == LocationNames.Start) continue;
            if (!Settings.IsTrackedItem(placedItems[placement.Index])) continue;

            treasures.Add(placement.Index);
        }

        List<int> randomOrder = [];
        for (int i = 0; i < treasures.Count; i++) randomOrder.Add(i);
        randomOrder.Shuffle(new(ctx.GenerationSettings.Seed + 21));
        Dictionary<int, int> randomDict = [];
        for (int i = 0; i < treasures.Count; i++) randomDict.Add(treasures[i], randomOrder[i]);

        treasures.StableSort((a, b) => CompareItems(ctx.itemPlacements[a].Item.Name, spheres[a], randomDict[a], ctx.itemPlacements[b].Item.Name, spheres[b], randomDict[b]));

        Treasures = [.. treasures];
        RemainingTreasures = [.. treasures];
        UpdateDisplayData();
    }
}
