# Biome Progression for Equipment

## Biome Themes

| Biome | Levels | Material / Aesthetic | Typical Elements |
|---|---|---|---|
| **Cave** | 1–25 | Stone, copper, bronze, leather, basic iron. Crude crafted look. | Mostly Neutral; Earth and Water appear later |
| **Forest** | 26–50 | Wood, hide, vine-wrapped iron, druidic motifs. | Earth, Wind |
| **Castle** | 51–75 | Steel, mithril, enchanted plate, ornate craft, holy/dark motifs. | Light, Dark (knightly/cleric vs wraith) |
| **Underworld** | 76–100+ | Obsidian, hellforged, demon-bone, infernal blue/black. Powerful late-game gear. | Fire, Dark |

## Rarity Distribution by Pit Level

| Pit Levels | Normal | Uncommon | Rare | Epic | Legendary |
|---|---|---|---|---|---|
| 1–10 | dominant | rare | very rare | almost never | never |
| 11–25 | common | common | rare | very rare | never |
| 26–50 | less common | common | normal | rare | very rare |
| 51–75 | uncommon | common | common | uncommon | rare |
| 76–100+ | rare | uncommon | common | common | uncommon |

The implementer wires this distribution through `BalanceConfig` / `CaveBiomeConfig` (and the equivalent biome configs once added).

## Boss-Tier Drops

- **Small boss (every 5):** guaranteed Uncommon or Rare drop, occasional Epic.
- **Big boss (every 25):** guaranteed Rare or Epic drop, occasional Legendary.
- Treasure transitions for Cave specifically:
  - Pit 1–10 always yields treasure level 1
  - Pit 11+: 35% non-boss / 60% boss weighted transition (see `CaveBiomeConfig.DetermineCaveTreasureLevel`)

## Equipment-Type Sliding Pool

Spawn pool is per-type. When designing a 5-level block, design **one of each type**:

```
Levels 1–5     weapons[1..5], armors[1..5], helms[1..5], shields[1..5], accessories[1..5]
Levels 6–10    weapons[1..10], … (10 of each type in pool)
Levels 11–15   weapons[6..15], … (drop first 5 of each, add 5 new)
```

## Elemental Tilt by Biome

Match the biome aesthetic to the elements used:

| Biome | Weapons | Armor | Notes |
|---|---|---|---|
| Cave | Neutral (early), Water/Earth (deep) | Neutral, Earth | Keep early gear simple |
| Forest | Earth, Wind | Earth, Wind | Druid/ranger flavor |
| Castle | Light (knight), Dark (wraith) | Light (paladin), Dark (necromancer) | Two opposing trees |
| Underworld | Fire, Dark | Fire, Dark | High-power late game |

Accessories can break the biome's elemental tilt for variety (a Fire ring in the Castle biome is fine).

## Hero Power Curve Awareness

The hero's stats grow per `PitHero/docs/JobStatCurves.md`. When designing for level N:

- A Normal weapon should match the hero's attack output at level N — not double it.
- An Uncommon weapon adds ~10–15% effective offense.
- A Rare weapon adds ~25–40% effective offense.
- An Epic weapon may exceed level expectations significantly (low drop rate compensates).
- A Legendary item should feel game-changing.

Cross-reference with `PitHero/docs/JobStatCurves.md` so the gear curve matches the hero's stat curve.
