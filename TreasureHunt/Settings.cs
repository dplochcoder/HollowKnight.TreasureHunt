using ConnectionMetadataInjector;
using ConnectionMetadataInjector.Util;
using ItemChanger;
using MenuChanger.Attributes;
using Newtonsoft.Json;
using RandomizerCore.Extensions;
using RandomizerMod.RC;
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

public class RandomizationSettings
{
    public bool Enabled;

    [PoolField] public bool TrueEnding;
    [PoolField] public bool Movement;
    [PoolField] public bool SwimAndIsmas;
    [PoolField] public bool Spells;
    [PoolField] public bool MajorKeys;
    [PoolField] public bool KeyLikeCharms;
    [PoolField] public bool FragileCharms;

    [ControlsField] [MenuRange(2, 6)] public int NumberOfReveals = 4;
    [ControlsField] public bool RollingWindow = false;

    public bool AltarOfDivination = false;
    [AltarField] public bool CurseOfWeakness = true;
    [AltarField] public bool CurseOfObsession = true;
    [AltarField] public bool CurseOfTheDamned = false;

    public int GetStableHashCode()
    {
        var strs = typeof(RandomizationSettings).GetFields().OrderBy(f => f.Name).Select(f => f.GetValue(this).ToString()).ToList();
        return string.Join(",", strs).GetStableHashCode();
    }

    private static HashSet<string> TrueEndingItems = [
        ItemNames.Dream_Nail, ItemNames.Dream_Gate, ItemNames.Awoken_Dream_Nail,
        ItemNames.Lurien, ItemNames.Monomon, ItemNames.Herrah, ItemNames.Dreamer,
        ItemNames.Queen_Fragment, ItemNames.King_Fragment, ItemNames.Void_Heart
    ];
    private static HashSet<string> MovementItems = [
        ItemNames.Left_Mothwing_Cloak, ItemNames.Right_Mothwing_Cloak, ItemNames.Split_Shade_Cloak, ItemNames.Mothwing_Cloak, ItemNames.Shade_Cloak,
        ItemNames.Left_Mantis_Claw, ItemNames.Right_Mantis_Claw, ItemNames.Mantis_Claw,
        ItemNames.Left_Crystal_Heart, ItemNames.Right_Crystal_Heart, ItemNames.Crystal_Heart,
        ItemNames.Monarch_Wings
    ];
    private static HashSet<string> SwimItems = [ItemNames.Swim, $"Not_{ItemNames.Swim}", ItemNames.Ismas_Tear, $"Not_{ItemNames.Ismas_Tear}"];
    private static HashSet<string> SpellItems = [
        ItemNames.Vengeful_Spirit, ItemNames.Shade_Soul,
        ItemNames.Desolate_Dive, ItemNames.Descending_Dark,
        ItemNames.Howling_Wraiths, ItemNames.Abyss_Shriek
    ];
    private static HashSet<string> MajorKeyItems = [
        ItemNames.Elevator_Pass, ItemNames.Elegant_Key, ItemNames.Love_Key, ItemNames.Tram_Pass, ItemNames.Kings_Brand
    ];
    private static HashSet<string> KeyLikeCharmItems = [
        ItemNames.Grimmchild1, ItemNames.Grimmchild2, ItemNames.Spore_Shroom, ItemNames.Defenders_Crest
    ];
    private static HashSet<string> FragileCharmItems = [
        ItemNames.Fragile_Greed, ItemNames.Unbreakable_Greed, ItemNames.Fragile_Heart, ItemNames.Unbreakable_Heart, ItemNames.Fragile_Strength, ItemNames.Unbreakable_Strength
    ];

    private static List<string> ItemPreferenceOrder = [
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

    private static bool IsUniqueKey(string name, IReadOnlyDictionary<string, AbstractItem> placedItems)
    {
        if (name == ItemNames.Simple_Key || name == ItemNames.Collectors_Map || name == ItemNames.Godtuner) return false;
        if (placedItems.TryGetValue(name, out var item))
        {
            var meta = SupplementalMetadata.Of(item);
            var poolGroup = meta.Get(InjectedProps.ItemPoolGroup);
            return poolGroup == PoolGroup.Keys.FriendlyName();
        }

        return false;
    }

    public bool IsTrackedItem(RandoModItem item, IReadOnlyDictionary<string, AbstractItem> placedItems)
    {
        if (TrueEnding && TrueEndingItems.Contains(item.Name)) return true;
        if (Movement && MovementItems.Contains(item.Name)) return true;
        if (Spells && SpellItems.Contains(item.Name)) return true;
        if (SwimAndIsmas && SwimItems.Contains(item.Name)) return true;
        if (MajorKeys && (MajorKeyItems.Contains(item.Name) || IsUniqueKey(item.Name, placedItems))) return true;
        if (KeyLikeCharms && KeyLikeCharmItems.Contains(item.Name)) return true;
        if (FragileCharms && FragileCharmItems.Contains(item.Name)) return true;

        return false;
    }

    public RandomizationSettings Clone() => (RandomizationSettings)MemberwiseClone();
}
