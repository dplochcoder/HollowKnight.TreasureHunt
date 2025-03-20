using MagicUI.Core;
using MagicUI.Elements;
using RandomizerMod.IC;
using System.Collections.Generic;

namespace TreasureHunt;

internal class TrackerUI
{
    private LayoutRoot layout;
    private List<TextObject> targets = [];

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
        for (int i = 0; i < 6; i++)
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

    private static string Clean(string name) => name.Replace("_", " ").Replace("-", " ");

    internal void Update(List<int> placementIndices)
    {
        Dictionary<int, string> strings = [];
        foreach (var p in ItemChanger.Internal.Ref.Settings.GetPlacements())
        {
            var tag = p.GetTag<RandoPlacementTag>();
            if (tag == null) continue;

            foreach (var id in tag.ids) strings[id] = Clean(p.Name);
        }

        List<string> displayStrings = [];
        foreach (var idx in placementIndices)
        {
            if (strings.TryGetValue(idx, out string name)) displayStrings.Add(name);
            else displayStrings.Add("??? Unknown Location ???");
        }
        displayStrings.Sort();

        for (int i = 0; i < targets.Count; i++) targets[i].Text = i < displayStrings.Count ? displayStrings[i] : "";
    }

    internal void Destroy() => layout.Destroy();
}
