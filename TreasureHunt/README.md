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

### Altar of Divination

The Altar of Divination is a relic located in Resting Grounds, just outside the tramway.

When all remaining known treasures are locked behind _non_-treasures, the player can perform a sacrifice at the altar to learn where the non-treasure(s) are located. The ritual has several requirements:

-   All alternatives must be exhausted. If any known treasure yet unobtained is logically accessible by the player, the ritual fails.
-   The player must be fully healed, and have no Shade.
-   Sufficient time must have passed. At least 30 minutes from the start of the seed, and at least 5 minutes since the completion of the previous ritual if any.
-   An offering of 1666 or less geo is required.

If all conditions are met, the ritual will divine a _singular_ unobtained item which is both logically accessible, and itself grants logical access to one or more unobtained treasures. The ritual may divine multiple items if a treasure exists at Nailmaster's Glory, and all remaining necessary Nail Arts are logically accessible. Otherwise, the ritual fails.

On ritual success, the player is _cursed_ in the following ways until all of the divined items are obtained:

-   Curse of weakness: The player takes +1 damage on all hits, before multipliers.
-   Curse of regret: The player's shade is sealed away in the altar and cannot be killed until the curse is lifted, or the player creates another Shade. The player does however keep their geo (minus offering) after sacrificing.
-   Curse of obsession: Obtaining any checks other than the divined ones inflicts 2 base damage (3 due to the curse of weakness, 6 if overcharmed).
-   Curse of the damned: The player may be periodically haunted by otherworldly foes.

Upon obtaining the divined items, all curses are lifted, and the Shade is returned to just above the altar. The player can perform multiple rituals if necessary, but the costs increase with each new ritual and the player must wait at least 5 minutes between rituals.
