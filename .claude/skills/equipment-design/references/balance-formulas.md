# Equipment Balance Formulas (Quick Reference)

Authoritative source: `EQUIPMENT_BALANCE_GUIDE.md` at the repo root. This is a concise lookup for design work.

## Core Formulas

`P` = pit level the equipment is balanced for. `rarity_mult` from the rarity table below.

| Component | Formula | Example (P=50, Normal) |
|---|---|---|
| Weapon Attack | `(1 + P / 2) * rarity_mult` | 26 |
| Armor Defense | `(1 + P / 3) * rarity_mult` | 17 |
| Accessory Stat | `(P / 5) * rarity_mult` | 10 |
| HP Bonus (VIT items) | `vit_stat * 5` | varies |
| MP Bonus (MAG items) | `mag_stat * 3` | varies |

## Rarity Multipliers

| Rarity | Multiplier |
|---|---|
| Normal | 1.0 |
| Uncommon | 1.2 |
| Rare | 1.5 |
| Epic | 2.0 |
| Legendary | 3.0 |

The implementer uses `BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity)` and similar helpers.

## Stat Caps to Respect

- Individual stats (STR/AGI/VIT/MAG) max 99
- HP max 9999, MP max 999
- All clamped via `StatConstants.ClampStat/HP/MP/StatBlock()`

## Elemental Tuning

Standard pattern for armored gear with own-element resistance:

```
Fire armor:  Resistances = { Fire: +0.30, Water: -0.15 }
Water armor: Resistances = { Water: +0.30, Fire: -0.15 }
Earth armor: Resistances = { Earth: +0.30, Wind: -0.15 }
Wind armor:  Resistances = { Wind: +0.30, Earth: -0.15 }
Light armor: Resistances = { Light: +0.30, Dark: -0.15 }
Dark armor:  Resistances = { Dark: +0.30, Light: -0.15 }
```

Weapons and accessories: pure element (no resistance entries).

## Power-Curve Sanity Check

For each new piece, compute the same-level hero's effective attack/defense and compare:

| Hero level | Knight ATK | Mage MAG | Knight VIT/HP | Reference rarity |
|---|---|---|---|---|
| 1 | low | low | low / ~30 HP | Normal weapon ~1 ATK |
| 25 | mid | mid | ~250 HP | Normal weapon ~13 ATK |
| 50 | mid-high | high | ~300 HP | Normal weapon ~26 ATK |
| 75 | high | very high | ~360 HP | Normal weapon ~38 ATK |
| 99 | 68 STR | 88 MAG | 78 VIT/415 HP | Normal weapon ~50 ATK |

(See `JOB_STAT_CURVES.md` for the per-job curves.)

A Normal-rarity weapon should add roughly **+50–100% to a same-level hero's effective offense** — never multiply it. Rare items may double it. Legendary items can triple or more.

## Worked Example

Design: **Iron Sword** at Pit 5, Uncommon, Neutral element.

- Attack = `(1 + 5/2) * 1.2 = 3.5 * 1.2 = 4` (rounded)
- HP/MP bonus: none
- StatBlock: `(0,0,0,0)`
- Element: Neutral, no resistances

Library entry stat block:

```
- Attack: 4
- Stats: 0 STR / 0 AGI / 0 VIT / 0 MAG
- Element: Neutral
- Rarity: Uncommon
- Pit level: 5
```

## Implementer Notes (informational only)

The implementer codes equipment as static factory methods (`public static Gear Create()`) using `BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity)`. Your design data must include `PitLevel` and `Rarity` so the formula resolves correctly on their end.
