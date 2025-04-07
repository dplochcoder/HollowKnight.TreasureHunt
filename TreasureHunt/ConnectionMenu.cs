using MenuChanger;
using MenuChanger.Extensions;
using MenuChanger.MenuElements;
using MenuChanger.MenuPanels;
using RandomizerMod.Menu;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TreasureHunt;

internal class ConnectionMenu
{
    internal static ConnectionMenu? Instance { get; private set; }

    internal static void Setup()
    {
        RandomizerMenuAPI.AddMenuPage(OnRandomizerMenuConstruction, TryGetMenuButton);
        MenuChangerMod.OnExitMainMenu += () => Instance = null;
    }

    internal static void OnRandomizerMenuConstruction(MenuPage page) => Instance = new(page);

    internal static bool TryGetMenuButton(MenuPage page, out SmallButton button)
    {
        button = Instance!.entryButton;
        return true;
    }

    private SmallButton entryButton;
    private MenuElementFactory<RandomizationSettings> factory;
    private List<ILockable> lockables = [];

    private ConnectionMenu(MenuPage landingPage)
    {
        MenuPage mainPage = new("Treasure Hunt Main Page", landingPage);
        entryButton = new(landingPage, "Treasure Hunt");
        entryButton.AddHideAndShowEvent(mainPage);

        factory = new(mainPage, TreasureHuntMod.GS.RS);
        var enabled = (factory.ElementLookup[nameof(RandomizationSettings.Enabled)] as MenuItem<bool>)!;
        enabled.SelfChanged += _ => SetLocksAndColor();

        foreach (var e in factory.ElementLookup)
        {
            if (e.Key == nameof(RandomizationSettings.Enabled)) continue;
            if (e.Value is ILockable l) lockables.Add(l);
        }

        var poolFields = factory.ElementLookup
            .Where(e => typeof(RandomizationSettings).GetField(e.Key, BindingFlags.Public | BindingFlags.Instance).GetCustomAttribute<PoolFieldAttribute>() != null)
            .Select(e => e.Value).ToArray();

        GridItemPanel pools = new(mainPage, SpaceParameters.TOP_CENTER_UNDER_TITLE, 4, SpaceParameters.VSPACE_SMALL, SpaceParameters.HSPACE_SMALL, false, poolFields);
        GridItemPanel controls = new(mainPage, SpaceParameters.TOP_CENTER_UNDER_TITLE + new Vector2(0, 100), 2, SpaceParameters.VSPACE_SMALL, SpaceParameters.HSPACE_MEDIUM, false, [
            factory.ElementLookup[nameof(RandomizationSettings.NumberOfReveals)],
            factory.ElementLookup[nameof(RandomizationSettings.RollingWindow)]]);
        VerticalItemPanel main = new(mainPage, SpaceParameters.TOP_CENTER_UNDER_TITLE, SpaceParameters.VSPACE_MEDIUM, true, [enabled, pools, controls]);

        main.Reposition();
        controls.MoveTo(new(0, 25));
        SetLocksAndColor();
    }

    internal void ApplySettings(RandomizationSettings settings) => factory.SetMenuValues(settings);

    private void SetLocksAndColor()
    {
        var enabled = TreasureHuntMod.GS.IsEnabled;
        entryButton.Text.color = TreasureHuntMod.GS.IsEnabled ? Colors.TRUE_COLOR : Colors.DEFAULT_COLOR;

        foreach (var lockable in lockables)
        {
            if (enabled) lockable.Unlock();
            else lockable.Lock();
        }
    }
}
