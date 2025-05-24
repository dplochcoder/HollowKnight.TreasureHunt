using DebugMod;

namespace TreasureHunt;

internal static class DebugInterop
{
    internal static void Setup() => DebugMod.DebugMod.AddToKeyBindList(typeof(DebugInterop));

    private static bool GetModule(out TreasureHuntModule mod)
    {
        mod = ItemChanger.ItemChangerMod.Modules.Get<TreasureHuntModule>();
        return mod != null;
    }

    [BindableMethod(name = "Advance Time", category = "Treasure Hunt")]
    public static void AdvanceTime()
    {
        if (!GetModule(out var mod)) return;

        mod.GameTime += 60 * 60;
        Console.AddLine("Advanced clock by 1 hour.");
    }

    [BindableMethod(name = "Grant Curse", category = "Treasure Hunt")]
    public static void GrantCurse()
    {
        if (!GetModule(out var mod) || mod.IsCurseActive()) return;

        mod.CursedIndices.Add(0);
        mod.UpdateDisplayData();
        Console.AddLine("Granted curse.");
    }

    [BindableMethod(name = "Remove Curse", category = "Treasure Hunt")]
    public static void RemoveCurse()
    {
        if (!GetModule(out var mod) || !mod.IsCurseActive()) return;

        mod.CursedIndices.Clear();
        mod.RemoveCurse();
        mod.UpdateDisplayData();
        Console.AddLine("Removed curse.");
    }

    [BindableMethod(name = "Reset Curse Count", category = "Treasure Hunt")]
    public static void ResetCurseCount()
    {
        if (!GetModule(out var mod)) return;

        mod.CompletedRituals = 0;
        Console.AddLine("Reset curse count.");
    }
}
