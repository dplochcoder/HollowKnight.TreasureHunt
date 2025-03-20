using ItemChanger;
using ItemChanger.Modules;
using RandomizerMod.IC;
using RandomizerMod.RC;
using System;
using System.Collections.Generic;

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
            if (toShow.Count == Settings.NumReveals) break;
            if (i >= RevealedTo)
            {
                if (Settings.RollingWindow) ++RevealedTo;
                else if (toShow.Count == 0) RevealedTo = Math.Min(RevealedTo + Settings.NumReveals, PlacementIndices.Count);
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

    internal void Start(List<ItemPlacement> placements)
    {
        Dictionary<string, AbstractItem> placedItems = [];
        foreach (var item in ItemChanger.Internal.Ref.Settings.GetItems()) placedItems[item.name] = item;

        foreach (var placement in placements)
        {
            if (placement.Location.Name == LocationNames.Start) continue;
            if (!Settings.IsTrackedItem(placement.Item, placedItems)) continue;

            PlacementIndices.Add(placement.Index);
        }

        PlacementIndices.Sort((a, b) => RandomizationSettings.CompareItems(placements[a].Item, placements[b].Item));
        UpdateRevealed();
    }
}
