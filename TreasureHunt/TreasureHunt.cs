using ItemChanger;
using ItemChanger.Internal.Menu;
using Modding;
using RandomizerMod.RC;
using System.Collections.Generic;
using UnityEngine;

namespace TreasureHunt;

public class TreasureHuntMod : Mod, IGlobalSettings<GlobalSettings>, ICustomMenuMod
{
    public static TreasureHuntMod? Instance;

    private static string version = PurenailCore.ModUtil.VersionUtil.ComputeVersion<TreasureHuntMod>();

    public override string GetVersion() => version;

    public TreasureHuntMod() : base("TreasureHunt")
    {
        Instance = this;
    }

    private static void HookRandoSettingsManager() => SettingsProxy.Setup();

    private static void HookDebugInterop() => DebugInterop.Setup();

    public override List<(string, string)> GetPreloadNames() => Preloader.Instance.GetPreloadNames();

    public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
    {
        Preloader.Instance.Initialize(preloadedObjects);

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
}
