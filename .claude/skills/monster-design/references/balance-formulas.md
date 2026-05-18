# Monster Balance Formulas (Quick Reference)

The authoritative source for formulas is `PitHero/docs/MonsterBalanceGuide.md`. This file is a concise lookup table for design work.

## Core Formulas

| Component | Formula | Example (Level 50) |
|---|---|---|
| Monster HP | `(10 + L * 5) * archetype_mult` | Balanced: 260 HP |
| Monster Stat | `(1 + L * 2/3) * archetype_mult` | Balanced: 34 stat |
| Monster XP | `10 + L * 8` | 410 XP |

`L` = monster level (typically equals pit level, +2 for boss-level scaling).

## Archetype Multipliers

| Archetype | HP × | Stat × | Profile |
|---|---|---|---|
| Balanced | 1.0 | 1.0 | All-rounder |
| Tank | 1.5 | 0.8 | High HP, low offense, high VIT |
| FastFragile | 0.6 | 1.2 | Low HP, high AGI, glass-cannon |
| MagicUser | 0.7 | 1.1 (MAG-biased) | Low VIT, high MAG, ranged threat |

When implementing, the planner-implementer uses `BalanceConfig.MonsterArchetype` to apply these.

## Boss Scaling

Boss-level monsters get **+2 levels** applied before `StatConstants.ClampLevel`. Use the level-shifted formulas when listing boss stats.

```
small boss at pit level 10 → L = 12 for stat calc
big boss at pit level 25 → L = 27 for stat calc
```

## Job Stat Benchmarks (Level 99) — for testing context

| Job | STR | AGI | VIT | MAG | HP | MP | Role |
|---|---|---|---|---|---|---|---|
| Knight | 68 | 42 | 78 | 28 | 415 | 94 | Tank |
| Monk | 73 | 62 | 58 | 37 | 315 | 121 | Balanced Fighter |
| Thief | 58 | 82 | 43 | 32 | 240 | 106 | Speed / Evasion |
| Archer | 62 | 72 | 48 | 37 | 265 | 121 | Ranged |
| Mage | 33 | 48 | 33 | 88 | 190 | 274 | Magic DPS |
| Priest | 38 | 53 | 43 | 78 | 240 | 244 | Healer |

Use these to sanity-check that high-level monsters challenge a level-matched hero without trivially out-stat-ing them.

## Elemental Tuning

- **Base multipliers** are 2.0× (advantage) and 0.5× (same element).
- **Custom resistances** (positive value reduces damage; negative increases):
  - Light defensive monster: `Fire: -0.30` weak, `Water: +0.30` resist
  - Heavy defensive monster: `Fire: -0.15, Water: +0.30`

Stick to the **±25–30% / ±10–15%** pattern unless you have a strong reason.

## Stat Caps to Respect

- HP max 9999, MP max 999
- Individual stats (STR/AGI/VIT/MAG) max 99
- Level max 99 (applies to monster level scaling too)

The implementer uses `StatConstants.ClampHP/MP/Stat/Level/StatBlock()` to enforce these.
