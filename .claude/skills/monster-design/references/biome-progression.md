# Biome Progression for Monsters

## Biome Themes

| Biome | Levels | Visual / Thematic Cues | Suggested Archetype Mix |
|---|---|---|---|
| **Cave** | 1–25 | Stone, dripping water, bats, slimes, ratlike creatures, kobolds. Early levels: weak, mostly Neutral. | Balanced (early), FastFragile (mid), Tank (boss) |
| **Forest** | 26–50 | Wolves, plants/treants, dryads, faeries, deer-like monsters, large bugs. Earth + Wind dominant. | Balanced, FastFragile, MagicUser (sprites) |
| **Castle** | 51–75 | Skeleton soldiers, knights, ghosts, gargoyles, golems, wraiths. Light + Dark dominant. | Tank, MagicUser, Balanced |
| **Underworld** | 76–100+ | Demons, fire elementals, hell hounds, fallen angels, abominations. Fire + Dark dominant. | MagicUser, Tank, FastFragile (high-end) |

## Boss Cadence

- **Every 5 levels:** small boss (slightly above the level's normal monsters; ~1.5× archetype multiplier)
- **Every 25 levels:** big boss = end of biome. Should feel like a major encounter; consider:
  - Larger sprite size (often Large 64×64)
  - Unique mechanic or elemental gimmick
  - Higher loot tier
- **Non-boss levels (e.g. 10, 11):** transitional, validate progression — use `Cave` biome boundaries as a reference (`PitHero/docs/CaveBiomeBalanceReport.md`)

## Spawn-Pool Sliding Window

```
Levels 1–5   pool = monsters[1..5]
Levels 6–10  pool = monsters[1..10]   ← introduces 5 new
Levels 11–15 pool = monsters[6..15]   ← drops first 5, adds 5 new
...
```

The pool is always **10 monsters** once past the first 5. When designing a new block:
- The 5 new monsters must complement (not duplicate) the 5 retained monsters.
- If the retained monsters cover Tank + Balanced, introduce more FastFragile / MagicUser variety.
- Boss-level monsters can be carried over for 5 levels (then rotate out).

## Sprite Sizes by Role

| Role | Recommended size |
|---|---|
| Standard monster (most) | Small (32×32) or Medium (48×48) |
| Small boss | Medium (48×48), occasionally Large |
| Big boss (every 25) | Large (64×64) |

## Element Themes by Biome

| Biome | Common elements | Sparingly used | Rare |
|---|---|---|---|
| Cave | Neutral, Earth | Water (deeper levels) | Wind / Fire |
| Forest | Earth, Wind | Water, Light | Fire / Dark |
| Castle | Light, Dark | Neutral, Earth | Fire / Water |
| Underworld | Fire, Dark | Light, Wind | Water / Earth |

Use this as a guide, not a rule — variety inside a biome makes spawn pools more interesting.
