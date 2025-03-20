using MenuChanger;
using MenuChanger.Extensions;
using MenuChanger.MenuElements;
using MenuChanger.MenuPanels;
using RandomizerMod.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    private ConnectionMenu(MenuPage landingPage)
    {
        MenuPage mainPage = new("Treasure Hunt Main Page", landingPage);
        entryButton = new(landingPage, "Treasure Hunt");
        entryButton.AddHideAndShowEvent(mainPage);

        factory = new(mainPage, TreasureHuntMod.GS.RS);
        new VerticalItemPanel(mainPage, SpaceParameters.TOP_CENTER_UNDER_TITLE, SpaceParameters.VSPACE_MEDIUM, true, factory.Elements);

        SetEnabledColor();
    }

    internal void ApplySettings(RandomizationSettings settings) => factory.SetMenuValues(settings);

    private void SetEnabledColor() => entryButton.Text.color = TreasureHuntMod.GS.IsEnabled ? Colors.TRUE_COLOR : Colors.DEFAULT_COLOR;
}
