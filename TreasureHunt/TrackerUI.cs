using ItemChanger;
using MagicUI.Core;
using MagicUI.Elements;
using RandomizerMod.IC;
using System;
using System.Collections.Generic;

namespace TreasureHunt;

internal class TrackerUI
{
    private LayoutRoot layout;
    private List<TextObject> targets = [];
    private List<(AbstractPlacement, Action<VisitStateChangedEventArgs>)> listeners = [];

    internal TrackerUI()
    {
        layout = new(true, "Treasure Hunt Tracker");
        layout.VisibilityCondition = () => GameManager.instance.isPaused || !TreasureHuntMod.GS.ShowPauseOnly;

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

    private static string Clean(string name) => name.Replace("_", " ").Replace("-", " ");

    internal void Update(List<int> placementIndices, int remaining, Dictionary<int, VisitState>? visitOverrides = null)
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
        foreach (var idx in placementIndices)
        {
            if (placements.TryGetValue(idx, out var placement))
            {
                var idxCopy = idx;
                Action<VisitStateChangedEventArgs> action = args => Update(placementIndices, remaining, new() { [idx] = args.NewFlags });
                listeners.Add((placement, action));
                placement.OnVisitStateChanged += action;

                string costTxt = "";
                if (visitOverrides == null || !visitOverrides.TryGetValue(idx, out var visitState)) visitState = placement.Visited;
                if ((visitState & VisitState.Previewed) == VisitState.Previewed)
                {
                    var cost = GetCost(placement, idx);
                    if (cost != null) costTxt = $" ({cost.GetCostText()})";
                }

                displayStrings.Add($"{Clean(placement.Name)}{costTxt}");
            }
            else displayStrings.Add("??? Unknown Location ???");
        }
        displayStrings.Add($"Treasure Remaining: {remaining}");

        for (int i = 0; i < targets.Count; i++) targets[i].Text = i < displayStrings.Count ? displayStrings[i] : "";
    }

    internal void Destroy() => layout.Destroy();
}
