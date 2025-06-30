using DebugMod;
using RandomizerMod.IC;
using TreasureHunt.IC;

namespace TreasureHunt.Interop;

internal static class DebugInterop
{
    private const string CATEGORY = "Treasure Hunt";

    internal static void Setup() => DebugMod.DebugMod.AddToKeyBindList(typeof(DebugInterop));

    private static bool GetModule(out TreasureHuntModule mod)
    {
#pragma warning disable CS8601 // Possible null reference assignment.
        mod = ItemChanger.ItemChangerMod.Modules.Get<TreasureHuntModule>();
#pragma warning restore CS8601 // Possible null reference assignment.
        return mod != null;
    }

    [BindableMethod(name = "Advance Time", category = CATEGORY)]
    public static void AdvanceTime()
    {
        if (!GetModule(out var mod)) return;

        mod.GameTime += 60 * 60;
        Console.AddLine("Advanced clock by 1 hour.");
    }

    [BindableMethod(name = "Obtain Accessible Treasure", category = CATEGORY)]
    public static void ObtainAccessibleTreasures()
    {
        if (!GetModule(out var mod)) return;

        // Find if any of the visible treasures are accessible.
        var rs = RandomizerMod.RandomizerMod.RS;
        var ctx = rs.Context;
        var pm = rs.TrackerData.pm;

        foreach (var idx in mod.GetVisibleTreasureIndices())
        {
            var randoPlacement = ctx.itemPlacements[idx];
            if (randoPlacement.Location.CanGet(pm))
            {
                foreach (var icPlacement in ItemChanger.Internal.Ref.Settings.Placements.Values)
                {
                    foreach (var item in icPlacement.Items)
                    {
                        if (item.GetTag<RandoItemTag>() is RandoItemTag tag && tag.id == idx)
                        {
                            item.Give(icPlacement, new()
                            {
                                Container = "TreasureHunt",
                                FlingType = ItemChanger.FlingType.DirectDeposit,
                                MessageType = ItemChanger.MessageType.Corner,
                                Transform = null,
                                Callback = null
                            });
                        }
                    }
                }

                return;
            }
        }
    }

    [BindableMethod(name = "Grant Curse", category = CATEGORY)]
    public static void GrantCurse()
    {
        if (!GetModule(out var mod) || mod.IsCurseActive()) return;

        var ctx = RandomizerMod.RandomizerMod.RS.Context;
        for (int i = 0; i < ctx.itemPlacements.Count; i++)
        {
            if (!mod.Acquired.Contains(i))
            {
                mod.GrantCurse([i]);
                Console.AddLine("Granted curse.");
                return;
            }
        }

        Console.AddLine("All items obtained.");
    }

    [BindableMethod(name = "Remove Curse", category = CATEGORY)]
    public static void RemoveCurse()
    {
        if (!GetModule(out var mod) || !mod.IsCurseActive()) return;

        mod.Curses.ForEach(c => c.CurseItems.Clear());
        mod.UpdateDisplayData();
        Console.AddLine("Removed curse.");
    }

    [BindableMethod(name = "Reset Curse Count", category = CATEGORY)]
    public static void ResetCurseCount()
    {
        if (!GetModule(out var mod)) return;

        mod.Curses.Clear();
        mod.UpdateDisplayData();
        Console.AddLine("Reset curse count.");
    }
}
