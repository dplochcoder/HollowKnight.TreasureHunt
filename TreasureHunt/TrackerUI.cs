using ItemChanger;
using MagicUI.Core;
using MagicUI.Elements;
using RandomizerMod.IC;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TreasureHunt;

internal class TrackerUI
{
    private LayoutRoot layout;
    private List<TextObject> targets = [];
    private TextObject cursedHeader;
    private List<TextObject> cursedTargets = [];
    private List<(AbstractPlacement, Action<VisitStateChangedEventArgs>)> listeners = [];

    internal TrackerUI()
    {
        layout = new(true, "Treasure Hunt Tracker");
        layout.VisibilityCondition = () => !TreasureHuntMod.GS.ShowPauseOnly || (GameManager.instance?.isPaused ?? false);

        StackLayout bigStack = new(layout, "Grid with Label")
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        bigStack.Children.Add(new TextObject(layout, "Grid Label")
        {
            Text = "Treasure Hunt Targets",
            FontSize = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });

        StackLayout smallStack = new(layout, "Targets")
        {
            Orientation = Orientation.Vertical,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        for (int i = 0; i < 7; i++)
        {
            TextObject target = new(layout, $"Target {i + 1}")
            {
                Text = "",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            targets.Add(target);
            smallStack.Children.Add(target);
        }
        bigStack.Children.Add(smallStack);

        cursedHeader = new(layout, "Cursed Grid Label")
        {
            Text = "",
            FontSize = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ContentColor = Color.red,
        };
        bigStack.Children.Add(cursedHeader);

        StackLayout cursedStack = new(layout, "Cursed Targets")
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        for (int i = 0; i < 3; i++)
        {
            TextObject cursedTarget = new(layout, $"Cursed Target {i + 1}")
            {
                Text = "",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            cursedTargets.Add(cursedTarget);
            cursedStack.Children.Add(cursedTarget);
        }
        bigStack.Children.Add(cursedStack);
    }

    private static Cost? GetCost(AbstractPlacement placement, int id)
    {
        foreach (var item in placement.Items)
        {
            var randoTag = item.GetTag<RandoItemTag>();
            if (randoTag == null || randoTag.id != id) continue;

            return item.GetTag<CostTag>()?.Cost;
        }

        return null;
    }

    internal static string Clean(string name) => name.Replace("_", " ").Replace("-", " ");

    private static string MinifyCostText(string txt) => txt.Replace("Once you own ", "Requires ").Replace(" charms, I'll gladly sell it to you.", " charms");

    private string ComputePlacementString(AbstractPlacement placement, int idx, DisplayData displayData, Dictionary<int, VisitState>? visitOverrides = null)
    {
        Action<VisitStateChangedEventArgs> action = args => Update(displayData, new() { [idx] = args.NewFlags });
        listeners.Add((placement, action));
        placement.OnVisitStateChanged += action;

        var cost = GetCost(placement, idx);
        string costTxt = "";
        if (cost != null)
        {
            if (visitOverrides == null || !visitOverrides.TryGetValue(idx, out var visitState)) visitState = placement.Visited;
            var previewed = (visitState & VisitState.Previewed) == VisitState.Previewed;

            var innerTxt = previewed ? MinifyCostText(cost.GetCostText()) : "???";
            costTxt = $" ({innerTxt})";
        }

        return $"{Clean(placement.Name)}{costTxt}";
    }

    internal void Update(DisplayData displayData, Dictionary<int, VisitState>? visitOverrides = null)
    {
        listeners.ForEach(pair =>
        {
            var (p, a) = pair;
            p.OnVisitStateChanged -= a;
        });
        listeners.Clear();

        Dictionary<int, AbstractPlacement> placements = [];
        foreach (var p in ItemChanger.Internal.Ref.Settings.GetPlacements())
        {
            var tag = p.GetTag<RandoPlacementTag>();
            if (tag == null) continue;

            foreach (var id in tag.ids) placements[id] = p;
        }

        List<string> displayStrings = [];
        foreach (var idx in displayData.treasures)
        {
            if (placements.TryGetValue(idx, out var placement)) displayStrings.Add(ComputePlacementString(placement, idx, displayData, visitOverrides));
            else displayStrings.Add("??? Unknown Location ???");
        }
        displayStrings.Add($"Treasure Remaining: {displayData.treasuresRemaining}");

        for (int i = 0; i < targets.Count; i++) targets[i].Text = i < displayStrings.Count ? displayStrings[i] : "";

        List<string> cursedStrings = [];
        foreach (var idx in displayData.cursed)
        {
            if (placements.TryGetValue(idx, out var placement)) cursedStrings.Add(ComputePlacementString(placement, idx, displayData, visitOverrides));
            else cursedStrings.Add("??? Unknown Location ???");
        }

        cursedHeader.Text = cursedStrings.Count > 0 ? "CURSED" : "";
        for (int i = 0; i < cursedTargets.Count; i++) cursedTargets[i].Text = i < cursedStrings.Count ? cursedStrings[i] : "";
    }

    internal void Destroy() => layout.Destroy();
}
