using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MenuChanger;
using MenuChanger.Extensions;
using MenuChanger.MenuElements;
using MenuChanger.MenuPanels;
using PurenailCore.SystemUtil;
using RandomizerMod.Menu;
using UnityEngine;

namespace TreasureHunt.Rando;

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

    private readonly SmallButton entryButton;
    private readonly MenuElementFactory<RandomizationSettings> factory;
    private readonly List<IMenuElement> hideables = [];
    private readonly List<IMenuElement> altarHideables = [];

    private static IValueElement[] FieldsWithAttr<T>(
        MenuElementFactory<RandomizationSettings> factory
    )
        where T : Attribute =>
        [
            .. factory
                .ElementLookup.Where(e =>
                    typeof(RandomizationSettings)
                        .GetField(e.Key, BindingFlags.Public | BindingFlags.Instance)
                        .GetCustomAttribute<T>() != null
                )
                .Select(e => e.Value),
        ];

    private static RandomizationSettings Settings => TreasureHuntMod.GS.RS;

    private ConnectionMenu(MenuPage landingPage)
    {
        MenuPage mainPage = new("Treasure Hunt Main Page", landingPage);
        entryButton = new(landingPage, "Treasure Hunt");
        entryButton.AddHideAndShowEvent(mainPage);

        factory = new(mainPage, Settings);
        var enabled = (
            factory.ElementLookup[nameof(RandomizationSettings.Enabled)] as MenuItem<bool>
        )!;
        enabled.SelfChanged += _ => SetVisibilityAndColor();

        var altar = (
            factory.ElementLookup[nameof(RandomizationSettings.AltarOfDivination)] as MenuItem<bool>
        )!;
        altar.SelfChanged += _ => SetVisibilityAndColor();

        foreach (var e in factory.ElementLookup)
        {
            if (e.Key == nameof(RandomizationSettings.Enabled))
                continue;
            if (e.Value is IMenuElement l)
            {
                if (
                    typeof(RandomizationSettings)
                        .GetField(e.Key, BindingFlags.Public | BindingFlags.Instance)
                        .GetCustomAttribute<AltarFieldAttribute>() != null
                )
                    altarHideables.Add(l);
                else
                    hideables.Add(l);
            }
        }

        var poolFields = FieldsWithAttr<PoolFieldAttribute>(factory);
        var controlsFields = FieldsWithAttr<ControlsFieldAttribute>(factory);
        var altarFields = FieldsWithAttr<AltarFieldAttribute>(factory);

        GridItemPanel pools = new(
            mainPage,
            SpaceParameters.TOP_CENTER_UNDER_TITLE,
            4,
            SpaceParameters.VSPACE_SMALL,
            SpaceParameters.HSPACE_SMALL,
            false,
            poolFields
        );
        List<GridItemPanel> controlsPanels =
        [
            .. controlsFields.Select(f => new GridItemPanel(
                mainPage,
                SpaceParameters.TOP_CENTER_UNDER_TITLE,
                1,
                SpaceParameters.VSPACE_SMALL,
                SpaceParameters.HSPACE_SMALL,
                false,
                f
            )),
        ];
        GridItemPanel altarControls = new(
            mainPage,
            SpaceParameters.TOP_CENTER_UNDER_TITLE,
            3,
            SpaceParameters.VSPACE_SMALL,
            SpaceParameters.HSPACE_SMALL,
            false,
            altarFields
        );
        VerticalItemPanel main = new(
            mainPage,
            SpaceParameters.TOP_CENTER_UNDER_TITLE,
            SpaceParameters.VSPACE_MEDIUM,
            true,
            [enabled, pools, .. controlsPanels, altar, altarControls]
        );
        main.Reposition();

        Vector2 offset = new(0, -SpaceParameters.VSPACE_MEDIUM);
        controlsPanels.ForEach(p => p.Translate(offset));
        altar.Translate(offset);
        altarControls.Translate(offset);

        SetVisibilityAndColor();
    }

    internal void ApplySettings(RandomizationSettings settings)
    {
        Settings.CopyFrom(settings);
        ;
        factory.SetMenuValues(settings);
        SetVisibilityAndColor();
    }

    private void SetVisibilityAndColor()
    {
        var enabled = TreasureHuntMod.GS.IsEnabled;
        var altar = TreasureHuntMod.GS.RS.AltarOfDivination;
        entryButton.Text.color = TreasureHuntMod.GS.IsEnabled
            ? Colors.TRUE_COLOR
            : Colors.DEFAULT_COLOR;

        foreach (var element in hideables)
        {
            if (enabled)
                element.Show();
            else
                element.Hide();
        }
        foreach (var element in altarHideables)
        {
            if (enabled && altar)
                element.Show();
            else
                element.Hide();
        }
    }
}
