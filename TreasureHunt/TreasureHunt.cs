using ItemChanger.Internal.Menu;
using Modding;
using PurenailCore.CollectionUtil;
using RandomizerMod.RC;
using System;
using System.Collections.Generic;
using TreasureHunt.IC;
using TreasureHunt.Interop;
using TreasureHunt.Rando;
using UnityEngine;

namespace TreasureHunt;

public class TreasureHuntMod : Mod, IGlobalSettings<GlobalSettings>, ICustomMenuMod
{
    public static TreasureHuntMod? Instance;

    private static readonly string Version = PurenailCore.ModUtil.VersionUtil.ComputeVersion<TreasureHuntMod>();

    public override string GetVersion() => Version;

    public TreasureHuntMod() : base("TreasureHunt")
    {
        Instance = this;
    }

    private static void HookRandoSettingsManager() => SettingsProxy.Setup();

    private static void HookDebugInterop() => DebugInterop.Setup();

    public override List<(string, string)> GetPreloadNames() => TreasureHuntPreloader.Instance.GetPreloadNames();

    public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
    {
        TreasureHuntPreloader.Instance.Initialize(preloadedObjects);

        ConnectionMenu.Setup();
        if (ModHooks.GetMod("RandoSettingsManager") is Mod) HookRandoSettingsManager();
        if (ModHooks.GetMod("DebugMod") is Mod) HookDebugInterop();

        RandoController.OnExportCompleted += OnExportCompleted;
        RandoController.OnCalculateHash += OnCalculateHash;
    }

    public static GlobalSettings GS = new();

    public bool ToggleButtonInsideMenu => throw new System.NotImplementedException();

    void IGlobalSettings<GlobalSettings>.OnLoadGlobal(GlobalSettings s) => GS = s;

    GlobalSettings IGlobalSettings<GlobalSettings>.OnSaveGlobal() => GS;

    private void OnExportCompleted(RandoController rc)
    {
        if (!GS.IsEnabled) return;

        var mod = ItemChanger.ItemChangerMod.Modules.GetOrAdd<TreasureHuntModule>();
        mod.Settings = GS.RS.Clone();
        mod.Start(rc.ctx!);
    }

    private int OnCalculateHash(RandoController rc, int orig)
    {
        if (!GS.IsEnabled) return 0;

        return GS.RS.GetStableHashCode();
    }

    public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates)
    {
        ModMenuScreenBuilder builder = new("Treasure Hunt", modListMenu);
        builder.AddHorizontalOption(new()
        {
            Name = "Show paused only",
            Description = "If yes, treasure hunt UI will only appear when game is paused.",
            Values = ["No", "Yes"],
            Saver = i => GS.ShowPauseOnly = i == 1,
            Loader = () => GS.ShowPauseOnly ? 1 : 0,
        });
        return builder.CreateMenuScreen();
    }

    private static readonly SortedMultimap<float, CalculateCurses> curseCalculators = new();
    internal static bool CalculateExtensionCurses(out List<int> cursePlacements)
    {
        foreach (var e in curseCalculators.AsDict) foreach (var v in e.Value) if (v(out cursePlacements)) return true;

        cursePlacements = [];
        return false;
    }

    private static readonly SortedMultimap<float, CalculateRitualCost> costCalculators = new();
    internal static void ModifyRitualCost(int completedRituals, ref int cost)
    {
        foreach (var e in costCalculators.AsDict) foreach (var v in e.Value) v(completedRituals, ref cost);
    }

    internal static bool InvokeOnIgnoreRitualTimeRequirement()
    {
        bool ignore = false;
        OnIgnoreRitualTimeRequirement?.Invoke(ref ignore);
        return ignore;
    }

    // Public API

    // Override the curse calculator for the Altar of Divination.
    public delegate bool CalculateCurses(out List<int> cursePlacements);

    // Override the cost calculator for curses.
    public delegate void CalculateRitualCost(int completedRituals, ref int cost);

    // If true, ignore time requirements on performing rituals.
    public delegate void IgnoreRitualTimeRequirement(ref bool ignore);

    public static void AddCurseCalculator(float priority, CalculateCurses curseCalculator) => curseCalculators.Add(priority, curseCalculator);
    public static void RemoveCurseCalculator(float priority, CalculateCurses curseCalculator) => curseCalculators.Remove(priority, curseCalculator);

    public static void AddCostCalculator(float priority, CalculateRitualCost costCalculator) => costCalculators.Add(priority, costCalculator);
    public static void RemoveCostCalculator(float priority, CalculateRitualCost costCalculator) => costCalculators.Remove(priority, costCalculator);

    public static event IgnoreRitualTimeRequirement? OnIgnoreRitualTimeRequirement;
}
