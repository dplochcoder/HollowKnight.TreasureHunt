# Treasure Hunt

A semi-spoiler rando connection which tells you where your next X major items are located.

The locations are revealed in progression-sphere order, with ties settled generally in favor of movement, TE, spells, etc. in that order.

## Settings

### Categories

Select and deselect categories to determine what items are counted as 'treasure'.

- True Ending: All dream nails, dreamers, and white fragments
- Movement: Dash, claw, cdash, wings
- Swim and Ismas: Swim and Isma's Tear
- Spells: Spells
- Major Keys: Elegant Key, Love Key, Tram Pass, Elevator Pass, King's Brand, and any self-identifying 'Key' from rando connections (e.g. MoreDoors, AccessRando)
- Key-like Charms: Grimmchild, Spore Shroom, Defender's Crest
- Fragile Charms: Fragile+Unbreakable Greed, Heart, Strength

### Number of Reveals

The number of category items to reveal simultaneously. Minimum 2, maximum 6.

### Rolling Window

Whether the selection of revealed category items updates immediately. If true, there will always be X revealed (as long as X are available).

If false, all X targets must be obtained before the next X are revealed.

### Tie Breaker Order

By default, "good" items\* are shown first when in the same progression sphere as other treasures.

You can change this setting to either reverse this ordering, or ignore it entirely and randomly sort items within the same spheres.

\* "Good" according to this author is approximately: Dash > Claw > Wings > CDash > Spells (Dive first) > Dream Nail > Swim > Dreamers > White Fragments > Various keys & key-like charms

### Altar of Divination

The Altar of Divination is a relic located in Resting Grounds, just outside the tramway.

When all remaining known treasures are locked behind _non_-treasures, the player can perform a sacrifice at the altar to learn where the non-treasure(s) are located. The ritual has several requirements:

-   All alternatives must be exhausted. If any known treasure yet unobtained is logically accessible by the player, the ritual fails.
-   The player must be fully healed, and have no Shade.
-   Sufficient time must have passed. At least 30 minutes from the start of the seed, and at least 5 minutes since the completion of the previous ritual if any.
-   An offering of 1666 or less geo is required.

If all conditions are met, the ritual will divine up to _four_ items, logically accessible, which when obtained grant access to one or more unobtained treasures. The ritual divines as few items as necessary to bring treasures into logic.

On ritual success, the player is _cursed_ in the following ways until either all of the divined items are obtained, or another treasure is obtained:

-   Curse of weakness: The player takes +1 damage on all hits, before multipliers.
-   Curse of mortality: The player slowly loses soul at all times.
-   Curse of regret: The player's shade is sealed away in the altar and cannot be killed until the curse is lifted, or the player creates another Shade. The player does however keep their geo (minus offering) after sacrificing.
-   Curse of obsession: Obtaining any checks other than the divined ones inflicts 2 base damage (3 due to the curse of weakness, 6 if overcharmed). Some exceptions exist to prevent unfair punishment.
-   Curse of the damned: The player may be periodically haunted by otherworldly foes. The foes have honor though will not pursue the player into a variety of places, including Godhome, White Palace, and all dream realms.

Upon obtaining the divined items, all curses are lifted, and the Shade is returned to just above the altar.

The player can perform multiple rituals if necessary, but the required offering grows larger, and the curses stronger, with each sequential ritual. The player must also wait at least 5 minutes between rituals.

## Connection Metadata

Other mods can interop with Treasure Hunt by providing a "TreasureHuntGroup" metadata property that exactly one of the seven listed categories. See `Settings.cs` for exact names.

If an item has an injected TreasureHuntGroup property, it will be treated as a treasure only if that setting is enabled. This _overrides_ base settings, allowing mods to also remove base items from Treasure Hunt groups if they so choose.

See [ConnectionMetadataInjector](https://github.com/BadMagic100/ConnectionMetadataInjector) for more details.
