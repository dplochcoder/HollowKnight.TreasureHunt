using ConnectionMetadataInjector;
using ConnectionMetadataInjector.Util;
using ItemChanger;
using MenuChanger.Attributes;
using Newtonsoft.Json;
using RandomizerCore.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TreasureHunt;

public class GlobalSettings
{
    public bool ShowPauseOnly = false;
    public RandomizationSettings RS = new();

    [JsonIgnore]
    public bool IsEnabled => RS.Enabled;
}

[AttributeUsage(AttributeTargets.Field)]
internal class PoolFieldAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field)]
internal class ControlsFieldAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field)]
internal class AltarFieldAttribute : Attribute { }

public enum TieBreakerOrder
{
    GoodItemsFirst,
    GoodItemsLast,
    Random
}

public class RandomizationSettings
{
    public const int MAX_REVEALS = 10;
    public const int MAX_CURSES = 4;

    public bool Enabled;

    [PoolField] public bool TrueEnding;
    [PoolField] public bool Movement;
    [PoolField] public bool Swimming;
    [PoolField] public bool Spells;
    [PoolField] public bool MajorKeys;
    [PoolField] public bool KeyLikeCharms;
    [PoolField] public bool FragileCharms;

    [ControlsField] [MenuRange(2, MAX_REVEALS)] public int NumberOfReveals = 4;
    [ControlsField] public bool RollingWindow = false;
    [ControlsField] public TieBreakerOrder TieBreaks = TieBreakerOrder.GoodItemsFirst;

    public bool AltarOfDivination = false;
    [AltarField] public bool CurseOfWeakness = true;
    [AltarField] public bool CurseOfObsession = true;
    [AltarField] public bool CurseOfTheDamned = false;

    public int GetStableHashCode()
    {
        var strs = typeof(RandomizationSettings).GetFields().OrderBy(f => f.Name).Select(f => f.GetValue(this).ToString()).ToList();
        return string.Join(",", strs).GetStableHashCode();
    }

    private static readonly HashSet<string> TrueEndingItems = [
        ItemNames.Dream_Nail, ItemNames.Dream_Gate, ItemNames.Awoken_Dream_Nail,
        ItemNames.Lurien, ItemNames.Monomon, ItemNames.Herrah, ItemNames.Dreamer,
        ItemNames.Queen_Fragment, ItemNames.King_Fragment, ItemNames.Void_Heart
    ];
    private static readonly HashSet<string> MovementItems = [
        ItemNames.Left_Mothwing_Cloak, ItemNames.Right_Mothwing_Cloak, ItemNames.Split_Shade_Cloak, ItemNames.Mothwing_Cloak, ItemNames.Shade_Cloak,
        ItemNames.Left_Mantis_Claw, ItemNames.Right_Mantis_Claw, ItemNames.Mantis_Claw,
        ItemNames.Left_Crystal_Heart, ItemNames.Right_Crystal_Heart, ItemNames.Crystal_Heart,
        ItemNames.Monarch_Wings
    ];
    private static readonly HashSet<string> SwimItems = [ItemNames.Swim, $"Not_{ItemNames.Swim}", ItemNames.Ismas_Tear, $"Not_{ItemNames.Ismas_Tear}"];
    private static readonly HashSet<string> SpellItems = [
        ItemNames.Vengeful_Spirit, ItemNames.Shade_Soul,
        ItemNames.Desolate_Dive, ItemNames.Descending_Dark,
        ItemNames.Howling_Wraiths, ItemNames.Abyss_Shriek
    ];
    private static readonly HashSet<string> MajorKeyItems = [
        ItemNames.Elevator_Pass, ItemNames.Elegant_Key, ItemNames.Love_Key, ItemNames.Tram_Pass, ItemNames.Kings_Brand
    ];
    private static readonly HashSet<string> KeyLikeCharmItems = [
        ItemNames.Grimmchild1, ItemNames.Grimmchild2, ItemNames.Spore_Shroom, ItemNames.Defenders_Crest
    ];
    private static readonly HashSet<string> FragileCharmItems = [
        ItemNames.Fragile_Greed, ItemNames.Unbreakable_Greed, ItemNames.Fragile_Heart, ItemNames.Unbreakable_Heart, ItemNames.Fragile_Strength, ItemNames.Unbreakable_Strength
    ];

    private static readonly List<string> ItemPreferenceOrder = [
        ItemNames.Left_Mothwing_Cloak, ItemNames.Right_Mothwing_Cloak, ItemNames.Split_Shade_Cloak, ItemNames.Mothwing_Cloak, ItemNames.Shade_Cloak,
        ItemNames.Left_Mantis_Claw, ItemNames.Right_Mantis_Claw, ItemNames.Mantis_Claw,
        ItemNames.Monarch_Wings,
        ItemNames.Left_Crystal_Heart, ItemNames.Right_Crystal_Heart, ItemNames.Crystal_Heart,
        ItemNames.Desolate_Dive, ItemNames.Descending_Dark,
        ItemNames.Vengeful_Spirit, ItemNames.Shade_Soul,
        ItemNames.Howling_Wraiths, ItemNames.Abyss_Shriek,
        ItemNames.Dream_Nail, ItemNames.Dream_Gate, ItemNames.Awoken_Dream_Nail,
        ItemNames.Swim, $"Not_{ItemNames.Swim}", ItemNames.Ismas_Tear, $"Not_{ItemNames.Ismas_Tear}",
        ItemNames.Lurien, ItemNames.Monomon, ItemNames.Herrah, ItemNames.Dreamer,
        ItemNames.Queen_Fragment, ItemNames.King_Fragment, ItemNames.Void_Heart,
        ItemNames.Tram_Pass, ItemNames.Kings_Brand,
        ItemNames.Grimmchild1, ItemNames.Grimmchild2, ItemNames.Spore_Shroom, ItemNames.Defenders_Crest,
        ItemNames.Fragile_Greed, ItemNames.Unbreakable_Greed, ItemNames.Fragile_Heart, ItemNames.Unbreakable_Heart, ItemNames.Fragile_Strength, ItemNames.Unbreakable_Strength
    ];

    internal static int CompareItemNames(string a, string b)
    {
        if (a == b) return 0;

        int idxA = ItemPreferenceOrder.IndexOf(a);
        int idxB = ItemPreferenceOrder.IndexOf(b);

        if (idxA == -1 && idxB == -1) return a.CompareTo(b);
        else if (idxA == -1) return 1;
        else if (idxB == -1) return -1;
        else return idxA - idxB;
    }

    private static bool IsUniqueKey(AbstractItem item)
    {
        if (item.name == ItemNames.Simple_Key || item.name == ItemNames.Collectors_Map || item.name == ItemNames.Godtuner) return false;
        
        var meta = SupplementalMetadata.Of(item);
        var poolGroup = meta.Get(InjectedProps.ItemPoolGroup);
        return poolGroup == PoolGroup.Keys.FriendlyName();
    }

    private const string None = "None";
    private static readonly MetadataProperty<AbstractItem, string> TreasureHuntGroup = new("TreasureHuntGroup", _ => None);
    private static readonly Dictionary<string, HashSet<string>> baseGroupSets = new()
    {
        [nameof(TrueEnding)] = TrueEndingItems,
        [nameof(Movement)] = MovementItems,
        [nameof(Spells)] = SpellItems,
        [nameof(Swimming)] = SwimItems,
        [nameof(MajorKeys)] = MajorKeyItems,
        [nameof(KeyLikeCharms)] = KeyLikeCharmItems,
        [nameof(FragileCharms)] = FragileCharmItems,
    };

    private static Dictionary<string, string> BuildBaseGroups()
    {
        Dictionary<string, string> ret = [];
        foreach (var e in baseGroupSets) foreach (var item in e.Value) ret.Add(item, e.Key);
        return ret;
    }
    private static readonly Dictionary<string, string> baseGroups = BuildBaseGroups();

    private bool IsGroupEnabled(string name) => name switch
    {
        nameof(TrueEnding) => TrueEnding,
        nameof(Movement) => Movement,
        nameof(Spells) => Spells,
        nameof(Swimming) => Swimming,
        nameof(MajorKeys) => MajorKeys,
        nameof(KeyLikeCharms) => KeyLikeCharms,
        nameof(FragileCharms) => FragileCharms,
        _ => false,
    };

    public bool IsTrackedItem(AbstractItem item)
    {
        var injectedGroup = SupplementalMetadata.Of(item).Get(TreasureHuntGroup);
        if (injectedGroup != None) return IsGroupEnabled(injectedGroup);

        if (baseGroups.TryGetValue(item.name, out var baseGroup)) return IsGroupEnabled(baseGroup);
        else return MajorKeys && IsUniqueKey(item);
    }

    public RandomizationSettings Clone() => (RandomizationSettings)MemberwiseClone();
}
